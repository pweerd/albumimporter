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
using FaceAiSharp;
using FaceAiSharp.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using Image = SixLabors.ImageSharp.Image;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace AlbumImporter {
   public class DetectedFace {
      public readonly float[] Embeddings;
      public readonly float DetectScore;

      public DetectedFace (float score, float[] embeddings) {
         DetectScore = score;
         Embeddings = embeddings;
      }

      public static bool IsValidForDetectedFace (float score, float[] embeddings) {
         return (score > 0 && embeddings != null && embeddings.Length > 0);
      }
   }
   public class KnownFace : DetectedFace {
      public readonly int Id;
      public KnownFace (FaceAiHelper hlp, int id, float score, float[] embeddings) : base (score, embeddings) {
         Id = id;
      }
      public KnownFace (int id, float score, float[] embeddings) : base (score, embeddings) {
         Id = id;
      }

      public static int SortId (KnownFace a, KnownFace b) {
         if (b.Id > a.Id) return -1;
         if (b.Id < a.Id) return 1;
         return 0;
      }

   }

   public class FaceHit {
      public readonly KnownFace DetectedFace;
      public readonly DetectedFace Face;
      public readonly float Score;
      public float CombinedScore => (float)Math.Sqrt (Score * Face.DetectScore * DetectedFace.DetectScore);

      public FaceHit (KnownFace known, DetectedFace arg, float score) {
         DetectedFace = known;
         Face = arg;
         Score = score;
      }

      public static int SortScore (FaceHit a, FaceHit b) {
         if (b.Score > a.Score) return 1;
         if (b.Score < a.Score) return -1;
         return 0;
      }

   }
   public class KnownFaces {
      private readonly FaceAiHelper hlp;
      private readonly KnownFace[] faces;
      public int Count => faces.Length;

      public KnownFaces (FaceAiHelper hlp, List<KnownFace> list) {
         this.hlp = hlp;
         faces = list.ToArray ();
         Array.Sort (faces, KnownFace.SortId);
      }

      public List<FaceHit> FindFaces (DetectedFace face) {
         var hits = new List<FaceHit> ();
         if (face == null) return hits;
         int prevId = -1;
         float maxScore = -1;
         KnownFace bestFace = null;
         float[] embeddings = face.Embeddings;
         for (int i = 0; i < faces.Length; i++) {
            var known = faces[i];
            var score = embeddings.Dot (known.Embeddings);
            if (score < .25f) continue;
            if (known.Id == prevId) {
               if (score > maxScore) {
                  maxScore = score;
                  bestFace = known;
               }
               continue;
            }
            if (bestFace != null) hits.Add (new FaceHit (bestFace, face, maxScore));
            maxScore = score;
            bestFace = known;
            prevId = known.Id;
         }
         if (bestFace != null) hits.Add (new FaceHit (bestFace, face, maxScore));
         if (hits.Count > 1) hits.Sort (FaceHit.SortScore);
         return hits;
      }
      public FaceHit FindFace (DetectedFace face) {
         var hits = new List<FaceHit> ();
         if (face == null) return null;
         float maxScore = -1;
         KnownFace bestFace = null;
         float[] embeddings = face.Embeddings;
         for (int i = 0; i < faces.Length; i++) {
            var known = faces[i];
            var score = embeddings.Dot (known.Embeddings);
            if (score < .25f) continue;
            if (score > maxScore) {
               maxScore = score;
               bestFace = known;
            }
         }
         return bestFace == null ? null : new FaceHit (bestFace, face, maxScore);
      }

   }

   public class DetectorResult {
      public readonly RectangleF Box;
      public readonly IReadOnlyList<PointF> LandmarksRO;
      public readonly PointF[] Landmarks;
      public readonly float Score;
      public DetectorResult (FaceDetectorResult r) {
         Box = r.Box;
         LandmarksRO = r.Landmarks;
         Landmarks = r.Landmarks.ToArray ();
         Score = (float)r.Confidence;
      }
   }


   public class FaceAiHelper : IDisposable {
      private readonly IFaceDetector _faceDetector;
      private readonly IFaceEmbeddingsGenerator _embeddingsGenerator;
      public FaceAiHelper () {
         _faceDetector = FaceAiSharpBundleFactory.CreateFaceDetector ();
         _embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator ();
      }

      public Image<Rgb24> LoadImage (byte[] bytes) {
         return Image.Load<Rgb24> (bytes);
      }
      public Image<Rgb24> LoadImage (Stream strm) {
         return Image.Load<Rgb24> (strm);
      }
      public Image<Rgb24> LoadImage (string fn) {
         return Image.Load<Rgb24> (fn);
      }


      public DetectedFace CreateFace (Image<Rgb24> img) {
         return _createFace (img);
      }
      public KnownFace CreateFace (Image<Rgb24> img, int id) {
         return (KnownFace)_createFace (img, id);
      }

      private DetectedFace _createFace (Image<Rgb24> img, int id = int.MinValue) {
         Image<Rgb24> img180 = null;
         try {
            var dr0 = DetectFace (img);
            var dr = dr0;
            if (dr0 != null && dr0.Score > .8f) goto DETECTED;
            img180 = img.Clone ();
            img180.Mutate (x => x.Rotate (180));

            var dr180 = DetectFace (img180);
            if (dr == null) {
               dr = dr180;
               img = img180;
            } else if (dr180 != null && dr180.Score > dr.Score) {
               dr = dr180;
               img = img180;
            }
            if (dr == null) return null;

            DETECTED:;
            Align (img, dr);
            var emb = CreateEmbedding (img);
            return id < 0 ? new DetectedFace (dr.Score, emb) : new KnownFace (this, id, dr.Score, emb);
         } finally {
            img180?.Dispose ();
         }
      }

      public DetectorResult DetectFace (Image<Rgb24> img) {
         var coll = _faceDetector.DetectFaces (img);
         switch (coll.Count) {
            case 0: return null;
            case 1:
               return new DetectorResult (coll.First ());
         }
         float score = float.MinValue;
         DetectorResult ret = null;
         foreach (var r in coll) {
            if (r.Confidence != null && r.Confidence > score) {
               score = (float)r.Confidence;
               ret = new DetectorResult (r);
            }
         }

         return ret;
      }

      public DetectorResult[] DetectFaces (Image<Rgb24> img) {
         var coll = _faceDetector.DetectFaces (img);
         return coll.Select (r => new DetectorResult (r)).ToArray ();
      }

      public int DetectFaceCount (Image<Rgb24> img) {
         return _faceDetector.DetectFaces (img).Count;
      }

      public void Align (Image<Rgb24> img, DetectorResult r) {
         _embeddingsGenerator.AlignFaceUsingLandmarks (img, r.LandmarksRO);
      }

      public float[] CreateEmbedding (Image<Rgb24> img) {
         return _embeddingsGenerator.GenerateEmbedding (img);
      }
      public float Compare (float[] a, float[] b) {
         return FaceAiSharp.Extensions.GeometryExtensions.Dot (a, b);
      }

      public Bitmap ToBitmap (Image<Rgb24> img) {
         var mem = new MemoryStream ();
         img.SaveAsBmp (mem);
         mem.Position = 0;
         return (Bitmap)System.Drawing.Image.FromStream (mem, false, false);
      }

      public void Dispose () {
      }
   }
}

