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
using Bitmanager.Json;
using System.Drawing;

namespace AlbumImporter {
   /// <summary>
   /// Determines the origin of face data.
   /// NB Everything bigger or equal to Manual survives a full import
   /// </summary>
   public enum NameSource { None, Unassigned, Auto, Manual, Error };

   /// <summary>
   /// Represents a face stored in the DB
   /// Note that we don't store the embeddings in the ES index, since records will be big and slow
   /// The embeddings are stored in a separate storage file
   /// A face is keyed like &lt;main-key&gt;~&lt;num&gt;
   /// </summary>
   public class DbFace {
      public string Id;
      public string User;
      public string InnerStr;
      public string RelPos;
      public string RelPos20;
      public List<FaceName> Names;
      public NameSource NameSrc;
      public float[] Embeddings;
      public int FaceCount;
      public int FaceStorageId;
      public int W0, H0;
      public float DetectedScore;

      public DbFace () {
         Names = new List<FaceName> ();
         DetectedScore = float.NaN;
      }
      public DbFace (string id): this() {
         Id = id;
      }



      public DbFace (GenericDocument rec) : this(rec.Id) {
         var src = rec._Source;
         FaceCount = src.ReadInt ("count", 0);
         User = src.ReadStr ("user", null);
         W0 = src.ReadInt ("w0", 0);
         H0 = src.ReadInt ("h0", 0);

         switch (src.ReadStr ("src", null)) {
            case "U": NameSrc = NameSource.Unassigned; break;
            case "M": NameSrc = NameSource.Manual; break;
            case "A": NameSrc = NameSource.Auto; break;
            case "E": NameSrc = NameSource.Error; break;
         }

         FaceStorageId = src.ReadInt ("storage_id", -1);
         InnerStr = src.ReadStr ("rect", "");
         RelPos = src.ReadStr ("relpos", "");

         var arr = src.ReadArr ("names", null);
         if (arr != null) {
            for (int i=0; i<arr.Count; i++)
               Names.Add (new FaceName ((JsonObjectValue)arr[i]));
         }
         arr = src.ReadArr ("embeddings", null);
         if (arr != null && arr.Count != 0) {
            Embeddings = new float[arr.Count];
            for (int i=0; i<arr.Count;i++)
               Embeddings[i] = (float)arr[i];
         }
         DetectedScore = src.ReadFloat ("detected_score", float.NaN);
      }

      public DbFace (string id, int count, int storageId, string innerStr, int w0, int h0, string relPos) : this(id) {
         FaceCount = count;
         FaceStorageId = storageId;
         InnerStr = innerStr;
         NameSrc = count > 0 ? NameSource.Unassigned : NameSource.None;
         W0 = w0;
         H0 = h0;
         RelPos = relPos;
      }

      public void UpdateNames (string[] definedNames) {
         for (int i = 0; i < Names.Count; i++) Names[i].UpdateName (definedNames);
      }

      public void Export (JsonObjectValue rec) {
         rec["_id"] = Id;
         rec["txt"] = Id;
         if (User != null) rec["user"] = User;
         if (Id[^1] == '0' && Id[^2] == '~') rec["first"] = true;
         if (FaceCount > 0) {
            rec["w0"] = W0;
            rec["h0"] = H0;
            rec["any_face"] = true;
            rec["count"] = FaceCount;
            rec["storage_id"] = FaceStorageId;
            rec["rect"] = InnerStr;
            rec["relpos"] = RelPos;
            rec["relpos20"] = RelPos20;

            switch (NameSrc) {
               case NameSource.None: break;
               case NameSource.Unassigned:
                  rec["src"] = "U";
                  break;
               case NameSource.Auto:
                  rec["src"] = "A";
                  break;
               case NameSource.Manual:
                  rec["src"] = "M";
                  break;
               case NameSource.Error:
                  rec["src"] = "E";
                  break;
            }
            if (Names?.Count > 0) {
               var arr = new JsonArrayValue ();
               foreach (var f in Names) arr.Add (f.ToJson ());
               rec["names"] = arr;
            }
            if (!float.IsNaN (DetectedScore)) rec["detected_score"] = DetectedScore;
            //The embeddings will never be exported 
            //Instead they are saved in a separate storage file
         }
      }

      internal void ClearNames () {
         Names.Clear ();
      }

      internal void AddName (int nameId, float score, float face1Score, float face2Score, float overallScore, string v) {
         Names.Add(new FaceName(nameId, score, face1Score, face2Score, overallScore, v));
      }

      public static int CBSortRelPos(DbFace a, DbFace b) {
         int rc = StringComparer.Ordinal.Compare (a.RelPos20, b.RelPos20);
         if (rc==0) StringComparer.Ordinal.Compare (a.RelPos, b.RelPos);
         return rc;
      }
   }
}
