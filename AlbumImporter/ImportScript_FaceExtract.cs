/*
 * Copyright © 2023, De Bitmanager
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Bitmanager.Core;
using Bitmanager.Elastic;
using Bitmanager.ImportPipeline;
using Bitmanager.Storage;
using Bitmanager.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace AlbumImporter {
   public class ImportScript_FaceExtract : ImportScriptBase {
      //We use PROCESSED as a mark that we processed the face from the prev db
      private static readonly float[] PROCESSED = new float[0];
      private FaceCollection existingFaces;
      private readonly FaceAiHelper hlp;
      private string[] faceNames;
      private int[] oldManualNameCounters; //Name stats for manual assigned faces
      private int[] newManualNameCounters;
      private readonly MemoryStream mem;
      private Storages storages;

      private HashSet<string> existingFaceRecIds;
      private Dictionary<string, DbFace> existingFacesByKeyAndLocation;

      private int lastFaceStorageId;

      //Stats for manual/error assigned faces
      private int oldErrorNames;
      private int oldManualNames;
      private int newErrorNames;
      private int newManualNames;

      public ImportScript_FaceExtract () {
         Configuration.Default.MaxDegreeOfParallelism = 1;
         hlp = new FaceAiHelper ();
         mem = new MemoryStream ();
      }
      public object OnDatasourceStart (PipelineContext ctx, object value) {
         Init (ctx, true);

         faceNames = ReadFaceNames();

         string url = base.copyFromUrl;
         if (url == null && !fullImport) url = base.oldIndexUrl;
         existingFaces = new FaceCollection (ctx.ImportLog, url, fullImport);
         //In case of a full import we reassign the ID's
         if (fullImport) {
            existingFacesByKeyAndLocation = new Dictionary<string, DbFace> ();
         } else {
            existingFaceRecIds = new HashSet<string> ();
            lastFaceStorageId = existingFaces.LargestStorageId;
         }
         ctx.ImportLog.Log ("FINGERPRINT: [{0}]", existingFaces.FingerPrint);

         oldManualNameCounters = new int[1+ faceNames.Length + 2];
         newManualNameCounters = new int[1+ faceNames.Length + 2];
         oldErrorNames = 0;
         oldManualNames = 0;
         newErrorNames = 0;
         newManualNames = 0;
         foreach (var f in existingFaces.GetFaces()) {
            int suffixIx = f.Id.LastIndexOf ('~');
            if (fullImport) {
               existingFacesByKeyAndLocation.Add (f.Id.Substring (0, suffixIx + 1) + f.RelPos, f);
            } else {
               existingFaceRecIds.Add (f.Id.Substring (0, suffixIx));
            }

            switch (f.NameSrc) {
               case NameSource.Error:
                  oldErrorNames++;
                  continue;
               case NameSource.Manual:
                  try {
                     if (f.Names.Count == 0) continue;
                     oldManualNameCounters[f.Names[0].Id]++;
                     oldManualNames++;
                     continue;
                  } catch { 
                     throw;
                  }
            }
         }

         if (fullImport) {
            ctx.ImportLog.Log ("Dumping existing IDs");
            foreach (var s in existingFaces.GetFaces().Select(f=>f.Id).OrderBy(s=>s))
               ctx.ImportLog.Log ("-- {0}", s);
         }

         //var fpIndex = Utils.GetIndexFingerPrint (ctx.Action.Endpoint);
         //sameIndex = fpIndex != null && fpIndex == existingFaces.FingerPrint;
         //ctx.ImportLog.Log ("same index: [{0}], fpIndex={1}, fp={2}", sameIndex, fpIndex, existingFaces.FingerPrint);

         logger.Log ("Loading/Creating bitmap storage");
         if (fullImport)
            storages = new Storages (faceAdminDir, newTimestamp, oldTimestamp);
         else
            storages = new Storages (faceAdminDir, newTimestamp);

         ctx.ImportLog.Log ("Starting faces extract. FullImport={0}, copy_from={1}, existing records={2}",
            fullImport,
            copyFromUrl,
            existingFaces.Count);

         handleExceptions = true;
         return null;
      }


      public object OnDatasourceEnd (PipelineContext ctx, object value) {
         var logger = ctx.ImportLog;
         handleExceptions = false;
         logger.Log ("Closing storage files");
         storages?.Dispose ();

         if (fullImport) {
            var type = oldErrorNames == newErrorNames && oldManualNames == newManualNames ? _LogType.ltDebug : _LogType.ltWarning;
            logger.Log (type, "-- Detected error-faces: {0}, was {1}", newErrorNames, oldErrorNames);
            logger.Log (type, "-- Detected manual-faces: {0}, was {1}", newManualNames, oldManualNames);
            for (int i = 0; i < newManualNameCounters.Length; i++) {
               if (newManualNameCounters[i] == oldManualNameCounters[i]) continue;
               var name = i < faceNames.Length ? faceNames[i] : null;
               logger.Log (_LogType.ltWarning, "-- -- ID={0:##}: new={1}, old={2}, name={3}", i, newManualNameCounters[i], oldManualNameCounters[i], name);
            }
            if (this.existingFacesByKeyAndLocation != null) {
               foreach (var kvp in this.existingFacesByKeyAndLocation) {
                  if (kvp.Value.Embeddings == PROCESSED) continue;
                  logger.Log (type, "-- Missed existing face. Src={0}, Key={1}", kvp.Value.NameSrc, kvp.Key);
               }
            }
         }

         try {
            if (esConnection != null && newIndex != null) {
               var syncher = new StorageSyncher (ctx.ImportLog, esConnection, newIndex);
               syncher.Synchronize (faceAdminDir);
            }
         } catch (Exception e) {
            ctx.ImportLog.Log (e, "Failed to synchronize: {0}", e.Message);
         }

         return null;
      }

      public object OnId (PipelineContext ctx, object value) {
         idInfo = (IdInfo)value;
         if (!File.Exists (idInfo.FileName)) {
            ctx.ActionFlags |= _ActionFlags.Skip;
            return null;
         }
         ctx.Action.Endpoint.Record["_id"] = idInfo.Id;
         List<DbFace> faces;
         if (fullImport) {
            //Full import
            faces = extractFaces (idInfo);
            if (faces[0].FaceCount > 0) combineExistingFaces (faces);
         } else {
            //Incr import
            if (existingFaceRecIds.Contains (idInfo.Id)) goto EXIT_RTN;
            faces = extractFaces (idInfo);
         }
         exportFaces (ctx, faces);
         WaitAfterExtract ();

      EXIT_RTN:
         return null;
      }


      private void combineExistingFaces (List<DbFace> faces) {
         try {
            string rootKey = idInfo.Id + "~";
            for (int i = 0; i < faces.Count; i++) {
               var face = faces[i];
               if (this.existingFacesByKeyAndLocation.TryGetValue(rootKey+face.RelPos, out var otherFace)) {
                  face.NameSrc = otherFace.NameSrc;
                  face.Names = otherFace.Names;
                  otherFace.Embeddings = PROCESSED; //Mark that we processed the face
               }
            }
         } catch (Exception e) {
            Debugger.Break ();
            throw;
         }
      }


      private void exportFaces (PipelineContext ctx, List<DbFace> list) {
         for (int i = 0; i < list.Count; i++) {
            var face = list[i];
            face.User = idInfo.User;
            switch (face.NameSrc) {
               case NameSource.Error:
                  newErrorNames++;
                  break;
               case NameSource.Manual:
                  newManualNames++;
                  newManualNameCounters[face.Names[0].Id]++;
                  break;
            }
            face.UpdateNames (faceNames);
            face.Export (ctx.Action.Endpoint.Record);
            ctx.Pipeline.HandleValue (ctx, "record/face", face);
         }
      }


      private static Image<Rgb24> extract (Image<Rgb24> srcImage, Rectangle srcRect) {
         Image<Rgb24> dstImage = new (srcRect.Width, srcRect.Height);

         int height = srcRect.Height;
         srcImage.ProcessPixelRows (dstImage, (srcAccessor, dstAccessor) => {
            for (int i = 0; i < height; i++) {
               Span<Rgb24> srcRow = srcAccessor.GetRowSpan (srcRect.Y + i);
               Span<Rgb24> dstRow = dstAccessor.GetRowSpan (i);

               srcRow.Slice (srcRect.X, srcRect.Width).CopyTo (dstRow);
            }
         });

         return dstImage;
      }

      private static Rectangle createLargerFaceRect(in RectangleF rc, int maxW, int maxH, out Rectangle innerFaceRect) {
         int deltaX = Math.Max (20, (int)(.4f * rc.Width));
         int deltaY = Math.Max (20, (int)(.3f * rc.Height));
         var left = roundDown (rc.X, deltaX);
         var top = roundDown (rc.Y, deltaY);
         var right = roundUp (rc.Right, deltaX, maxW);
         var bot = roundUp (rc.Bottom, deltaY, maxH);
         innerFaceRect = new Rectangle (
            (int)(.5 + rc.X - left),
            (int)(.5 + rc.Y - top),
            (int)(.5 + rc.Width),
            (int)(.5 + rc.Height)
         );
         return new Rectangle (left, top, right - left, bot - top);
      }

      private static void scaleRect (ref Rectangle rc, float scale) {
         rc.X = (int)Math.Floor (rc.X * scale);
         rc.Y = (int)Math.Floor (rc.Y * scale);
         rc.Width = (int)Math.Ceiling (rc.Width * scale);
         rc.Height = (int)Math.Ceiling (rc.Height * scale);
      }
      private List<DbFace> extractFaces (IdInfo idInfo) {
         string idPrefix = idInfo.Id + "~"; 
         var jpgEncoder = new JpegEncoder () { Quality = 90 };
         var dbFaces = new List<DbFace> ();
         Image<Rgb24> img = null;
         Image<Rgb24> imgFace = null;
         const int MAX = 250;
         try {
            img = hlp.LoadImage (idInfo.FileName);
            int imgW = img.Width;
            int imgH = img.Height;
            var detResults = hlp.DetectFaces (img);
            DbFace dbFace;
            if (detResults == null || detResults.Length == 0) {
               dbFaces.Add (new DbFace (idPrefix + "0"));
               return dbFaces;
            }

            for (int i = detResults.Length; i > 0;) {
               var detResult = detResults[--i];
               var largerRect = createLargerFaceRect (detResult.Box, imgW, imgH, out var innerFaceRect);
               imgFace?.Dispose ();
               imgFace = extract (img, largerRect);

               //Downscale if needed
               int w = imgFace.Width;
               int h = imgFace.Height;
               float scaleFactor  = Math.Min(MAX / (float)w, MAX / (float)h);
               if (scaleFactor < 1f) {
                  imgFace.Mutate (x => x.Resize ((int)(w* scaleFactor), (int)(h * scaleFactor), KnownResamplers.Lanczos3));
                  scaleRect (ref innerFaceRect, scaleFactor);
               }

               int storId = ++lastFaceStorageId;
               string storKey = storId.ToString ();

               mem.SetLength (0);
               imgFace.SaveAsJpeg (mem, jpgEncoder);
               storages.CurrentFaceStorage.AddBytes (mem.GetBuffer (), 0, (int)mem.Length, storKey, DateTime.UtcNow, CompressMethod.Store);

               //Create and save the embeddings
               var cloned = (i == 0) ? img : img.Clone ();
               hlp.Align (cloned, detResult);
               var embeddings = hlp.CreateEmbedding (cloned);
               var bytes = BufferHelper.ToByteArray (embeddings);
               storages.CurrentEmbeddingStorage.AddBytes (bytes, 0, bytes.Length, storKey, DateTime.UtcNow, CompressMethod.Deflate);

               dbFace = new DbFace ();
               dbFace.DetectedScore = detResult.Score;
               dbFace.FaceCount = detResults.Length;
               dbFace.FaceStorageId = storId;
               dbFace.W0 = imgFace.Width;
               dbFace.H0 = imgFace.Height;
               dbFace.RelPos = createRelpos (largerRect, imgW, imgH, out dbFace.RelPos20);
               dbFace.InnerStr = createInnerStr (innerFaceRect);
               dbFaces.Add (dbFace);
               if (cloned != img) cloned.Dispose ();
               cloned = null;
            }

            //Sort them on relpos, in order to keep the ID's mostly the same on a full import
            if (dbFaces.Count > 1) dbFaces.Sort (DbFace.CBSortRelPos);
            //and assign IDs
            for (int i=0; i<dbFaces.Count; i++) dbFaces[i].Id = idPrefix + i;
            return dbFaces;
         } finally {
            img?.Dispose ();
         }
      }

      private static string createInnerStr (in Rectangle rc) {
         return Invariant.Format ("{0},{1},{2},{3}", rc.X, rc.Y, rc.Width, rc.Height);
      }

      private static string createRelpos (in Rectangle largerRect, int imgW, int imgH, out string relpos20) {
         float x = largerRect.X / (float)imgW;
         float w = largerRect.Width / (float)imgW;
         float y = largerRect.Y / (float)imgH;
         float h = largerRect.Height / (float)imgH;

         relpos20 = Invariant.Format ("{0:D2},{1:D2},{2:F5},{3:F5}", (int)(x*20), (int)(y*20), w, h);
         return Invariant.Format ("{0:F5},{1:F5},{2:F5},{3:F5}", x, y, w, h);
      }

      private static int roundDown (float f, int delta) {
         var x = ((int)(f)) - delta;
         return x < 0 ? 0 : x;
      }
      private static int roundUp (float f, int delta, int max) {
         var x = ((int)(.5f + f)) + delta;
         return x > max ? max : x;
      }


   }
}
