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
using Bitmanager.Xml;

namespace AlbumImporter {
   public class ImportScript_FaceMatcher : ImportScriptBase {
      private List<DbFace> existingFaces;
      private string[] faceNames;
      private readonly FaceAiHelper hlp;
      private Storages storages;

      public ImportScript_FaceMatcher () {
         hlp = new FaceAiHelper ();
      }


      public object OnDatasourceStart (PipelineContext ctx, object value) {
         Init (ctx, true);

         faceNames = ReadFaceNames ();

         logger.Log ("Fetching existing faces");
         string url = base.copyFromUrl;
         if (url == null && !fullImport) url = base.oldIndexUrl;
         existingFaces = new FaceCollection (ctx.ImportLog, url).GetFaces();

         logger.Log ("Loading storages");
         storages = fullImport 
            ? new Storages (faceAdminDir, newTimestamp, oldTimestamp)
            : new Storages (faceAdminDir, newTimestamp);


         logger.Log ("Loading embeddings from storage");
         foreach (var f in existingFaces) assignEmbedding (f);


         var knownFaces = loadKnownFaces (existingFaces, out var updatedFaces);

         ctx.ImportLog.Log ("Starting face matching. FullImport={0}, known faces={1}, updated faces={2}",
            fullImport,
            knownFaces.Count, 
            updatedFaces.Count);

         handleExceptions = true;

         var ep = ctx.Action.Endpoint;
         if (fullImport) {  //Full import: we need to emit *all* records and copy *all* embeddings/face-imgs
            foreach (var f in existingFaces) {
               if (f.NameSrc == NameSource.Error || f.NameSrc == NameSource.Manual) {
                  goto EMIT_FACE_FULL;
               }
               if (f.FaceCount == 0) goto EMIT_FACE_FULL;

               if (!DetectedFace.IsValidForDetectedFace (f.DetectedScore, f.Embeddings))
                  throw new BMException ("Normal face [{0}] has no embeddings or Detected score.", f.Id);

               DetectedFace face = new DetectedFace (f.DetectedScore, f.Embeddings);

               var hits = knownFaces.FindFaces (face);
               if (hits.Count == 0) goto EMIT_FACE_FULL;

               f.ClearNames ();
               int nameId = hits[0].DetectedFace.Id;
               f.AddName (nameId,
                  hits[0].Score,
                  hits[0].Face.DetectScore,
                  hits[0].DetectedFace.DetectScore,
                  hits[0].CombinedScore,
                  nameId < faceNames.Length ? faceNames[nameId] : null);
               f.NameSrc = NameSource.Auto;


            EMIT_FACE_FULL:
               if (f.FaceStorageId > 0) {
                  string key = f.FaceStorageId.ToString ();
                  storages.CopyOldToCur (key, f.Id);
               }
               f.Export (ep.Record);
               ctx.Pipeline.HandleValue (ctx, "record", ep.Record);
            }
         } else { //Incremental import: only emit assigned faces
            //Export updated faces
            for (int i = 0; i < updatedFaces.Count; i++) {
               updatedFaces[i].Export (ep.Record);
               ctx.Pipeline.HandleValue (ctx, "record", ep.Record);
            }

            foreach (var f in existingFaces) {
               if (f.NameSrc == NameSource.Error || f.NameSrc == NameSource.Manual) {
                  continue;
               }
               if (f.FaceCount == 0) continue;

               if (!DetectedFace.IsValidForDetectedFace (f.DetectedScore, f.Embeddings)) {
                  string msg = Invariant.Format ("Normal face [{0}] has no embeddings or Detected score.", f.Id);
                  Logs.ErrorLog.Log (msg);
                  continue;
               }

               DetectedFace face = new DetectedFace (f.DetectedScore, f.Embeddings);

               var hits = knownFaces.FindFaces (face);
               if (hits.Count == 0) continue;

               f.ClearNames ();
               int nameId = hits[0].DetectedFace.Id;
               f.AddName (nameId,
                  hits[0].Score,
                  hits[0].Face.DetectScore,
                  hits[0].DetectedFace.DetectScore,
                  hits[0].CombinedScore,
                  nameId < faceNames.Length ? faceNames[nameId] : null);
               f.NameSrc = NameSource.Auto;

               f.Export (ep.Record);
               ctx.Pipeline.HandleValue (ctx, "record", ep.Record);
            }
         }
         return null;
      }


      public object OnDatasourceEnd (PipelineContext ctx, object value) {
         handleExceptions = false;
         ctx.ImportLog.Log ("Closing storage file(s)");
         storages?.Dispose ();

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


      private void assignEmbedding (DbFace face) {
         if (face.Embeddings != null || face.FaceStorageId <= 0) return;
         var bytes = storages.OldEmbeddingStorage.GetBytes (face.FaceStorageId.ToString (), false);
         face.Embeddings = BufferHelper.FromByteArray<float> (bytes);
         if (!DetectedFace.IsValidForDetectedFace (face.DetectedScore, face.Embeddings)) {
            string msg = Invariant.Format ("Face [{0}] has no embeddings or Detected score.", face.Id);
            Logs.ErrorLog.Log (msg);
         }
      }
      private KnownFaces loadKnownFaces (List<DbFace> faces, out List<DbFace> updatedFaces) {
         var list = new List<KnownFace> ();
         var upd = new List<DbFace> ();
         foreach (var f in faces) {
            if (f.NameSrc != NameSource.Manual) continue;
            if (f.Names.Count == 0) {
               string msg = Invariant.Format ("No names for [{0}].", f.Id);
               Logs.ErrorLog.Log (msg);
               continue;
            }
            if (!DetectedFace.IsValidForDetectedFace (f.DetectedScore, f.Embeddings)) {
               string msg = Invariant.Format ("Manual face [{0}] has no embeddings or Detected score.", f.Id);
               Logs.ErrorLog.Log (msg);
               continue;
            }

            KnownFace known = new KnownFace (f.Names[0].Id, f.DetectedScore, f.Embeddings);
            if (f.Names[0].Score != 1) {
               f.Names[0].Score = 1;
               upd.Add (f);
            }
            list.Add (known);
         }
         updatedFaces = upd;
         return new KnownFaces (hlp, list);
      }


   }

}
