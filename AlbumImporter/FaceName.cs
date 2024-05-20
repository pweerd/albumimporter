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

using Bitmanager.Json;

namespace AlbumImporter {
   
   /// <summary>
   /// Represents a known or a matched face
   /// </summary>
   public class FaceName {
      public string Name;
      public readonly int Id;
      public float Score;
      public float FaceDetectScore;
      public float DetectedFaceDetectScore;
      public float OverallScore;

      public FaceName (int id, float score, float faceDetScore, float detFaceDetScore, float overallScore, string name) {
         Id = id;
         Score = score;
         FaceDetectScore = faceDetScore;
         DetectedFaceDetectScore = detFaceDetScore;
         OverallScore = overallScore;
         Name = name;
      }
      public FaceName (JsonObjectValue v) {
         Id = v.ReadInt("id");

         Score = v.ReadFloat ("match_score", float.NaN);
         if (float.IsNaN(Score)) Score = v.ReadFloat ("score");

         FaceDetectScore = v.ReadFloat ("face_detect_score", float.NaN);
         if (float.IsNaN (FaceDetectScore)) FaceDetectScore = v.ReadFloat ("score1", float.NaN);

         DetectedFaceDetectScore = v.ReadFloat ("detected_face_detect_score", float.NaN);
         if (float.IsNaN (DetectedFaceDetectScore)) DetectedFaceDetectScore = v.ReadFloat ("score2", float.NaN);

         OverallScore = v.ReadFloat ("score_all", float.NaN);
         Name = v.ReadStr("name", null);
      }

      public void ClearDetectedNameInfo () {
         Score = float.NaN;
         DetectedFaceDetectScore = float.NaN;
         OverallScore = float.NaN;
         Name = null;
      }
      public void UpdateName (string[] faceNames) {
         Name = (Id < 0 || Id >= faceNames.Length) ? null : faceNames[Id];
      }

      public JsonObjectValue ToJson() {
         var json = new JsonObjectValue();
         json.Add ("id", Id);
         json.Add ("match_score", Score);
         if (!float.IsNaN (FaceDetectScore)) json.Add ("face_detect_score", FaceDetectScore);
         if (!float.IsNaN (DetectedFaceDetectScore)) json.Add ("detected_face_detect_score", DetectedFaceDetectScore);
         if (!float.IsNaN (OverallScore)) json.Add ("score_all", OverallScore);
         if (!string.IsNullOrEmpty(Name)) json.Add ("name", Name);
         return json;
      }

   }
}
