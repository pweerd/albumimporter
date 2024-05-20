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
using Bitmanager.Gps;
using Bitmanager.ImportPipeline;
using Bitmanager.IO;
using Bitmanager.Json;
using Bitmanager.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace AlbumImporter {

   class TrackAdmin {
      public readonly string Id;
      public readonly string Timezone;
      public readonly DateTime StartTimeLimit, EndTimeLimit;
      public readonly float StartLat, StartLon;
      public readonly float EndLat, EndLon;

      public TrackAdmin (string id, Track t) {
         Id = id;
         Timezone = t.TimeZoneId;
         int secs = t.IncludePhotosBeforeSec;
         if (secs < 0) secs = 30000;
         StartTimeLimit = t.StartTime.AddSeconds (-secs);

         secs = t.IncludePhotosAfterSec;
         if (secs < 0) secs = 7200;
         EndTimeLimit = t.EndTime.AddSeconds (secs);
         StartLat = (float)t.Items[0].Lat;
         StartLon = (float)t.Items[0].Lon;
         EndLat = (float)t.Items[^1].Lat;
         EndLon = (float)t.Items[^1].Lon;
      }
   }


   class TrackPoint {
      public readonly TrackAdmin Track;
      public readonly DateTime Time;
      public readonly float Lat, Lon;
      public TrackPoint (TrackItem item) {
         Time = item.Time;
         Lat = (float)item.Lat;
         Lon = (float)item.Lon;
      }
      public TrackPoint (TrackItem item, TrackAdmin track) {
         Time = item.Time;
         Lat = (float)item.Lat;
         Lon = (float)item.Lon;
         Track = track;
      }
   }

   class NameAndDate {
      public static readonly IComparer<DateTime> dtComparer = Comparer<DateTime>.Default;
      public static readonly IComparer<string> strComparer = StringComparer.InvariantCulture;
      public readonly string Name;
      public readonly DateTime Time;
      public double Lat, Lon;
      public TrackAdmin AssociatedTrack;

      public NameAndDate () { //Creates a dummy item
         Lat = Lon = double.NaN;
      }
      public NameAndDate (string name, DateTime date) {
         Name = Path.GetFileNameWithoutExtension (name).ToLowerInvariant ();
         Time = date;
         Lat = Lon = double.NaN;
      }

      public static int CbSortDate (NameAndDate x, NameAndDate y) {
         int rc = dtComparer.Compare (x.Time, y.Time);
         if (rc == 0) rc = strComparer.Compare (x.Name, y.Name);
         return rc;
      }
      public static int CbSortName (NameAndDate x, NameAndDate y) {
         return strComparer.Compare (x.Name, y.Name);
      }

      public override string ToString () {
         var sb = new StringBuilder ();
         sb.Append (Name).Append (';').Append (Time);
         if (!double.IsNaN (Lat) && !double.IsNaN (Lon)) {
            sb.Append (": lat=").Append (Lat).Append (", lon=").Append (Lon).Append (", trk=").Append (AssociatedTrack);
         }
         return sb.ToString ();
      }

      public void ExportToJson (JsonObjectValue v) {
         v["_id"] = Name;
         v["lat"] = Lat;
         v["lon"] = Lon;
         v["trkid"] = AssociatedTrack.Id;
         v["tz"] = AssociatedTrack.Timezone;
      }
   }


   public class ImportScript_Tracks {
      public object OnDatasourceStart (PipelineContext ctx, object value) {
         string tracksUrl = ctx.Action.Node.ReadStr ("@url");

         var req = Utils.CreateESRequest (tracksUrl);
         req.Query = new ESTermQuery ("", "");

         ctx.ImportLog.Log ("Loading tracks...");
         var points = loadTrackPoints (req);
         ctx.ImportLog.Log ("Loaded {0} track points", points.Count);
         ctx.ImportLog.Log ("Loading photos...");
         var photos = loadTrackPhotos (req);
         ctx.ImportLog.Log ("Loaded {0} photos", photos.Count);

         ctx.ImportLog.Log ("Determine lat/lon...");
         mergePhotos (photos, points);
         ctx.ImportLog.Log ("Intrapolate positions for unknown photos...");
         includeStartEndPhotos (photos);

         ctx.ImportLog.Log ("export photos...");
         using (var fs = IOUtils.CreateOutputStream(@"z:\photos.txt")) {
            var wtr = new JsonWriter (fs, false);
            wtr.Formatted = true;

            var ep = ctx.Action.Endpoint;
            for (int i = 0; i < photos.Count; i++) {
               var photo = photos[i];
               if (photo.AssociatedTrack == null) continue;

               ctx.IncrementEmitted ();
               var json = ep.Record;
               photo.ExportToJson (json);
               ctx.Pipeline.HandleValue (ctx, "record", json);
               json.WriteTo (wtr);
               wtr.WriteRaw ("\n");
            }

         }

         return null;
      }

      private void mergePhotos (List<NameAndDate> photos, List<TrackPoint> points) {
         int N = photos.Count;
         if (N == 0) return;
         int M = points.Count;
         if (M <= 1) return;

         int i = 0, j = 1;
         while (i < N && j < M) {
            if (points[j].Time < photos[i].Time) {
               ++j;
               continue;
            }
            if (points[j - 1].Time > photos[i].Time) {
               ++i;
               continue;
            }
            setLatLon (photos[i], points[j], points[j - 1]);
            ++i;

         }
      }

      private static void includeStartEndPhotos (List<NameAndDate> photos) {
         if (photos.Count == 0) return;
         int firstNonAssociated = -1;
         NameAndDate prev = new NameAndDate ();
         for (int i = 0; i < photos.Count; ++i) {
            var photo = photos[i];
            if (photo.AssociatedTrack == null) {
               //Try to associate with the last resolved photo, checking the end-limit
               if (prev.AssociatedTrack != null) {
                  if (photo.Time <= prev.AssociatedTrack.EndTimeLimit) {
                     photo.AssociatedTrack = prev.AssociatedTrack;
                     photo.Lat = prev.AssociatedTrack.EndLat;
                     photo.Lon = prev.AssociatedTrack.EndLon;
                     continue;
                  }
               }
               if (firstNonAssociated < 0) firstNonAssociated = i;
               continue;
            }

            //We have an associated photo here
            prev = photo;
            if (firstNonAssociated >= 0) {
               //Try to associate with the current photo, checking the start-limit
               DateTime limit = photo.AssociatedTrack.StartTimeLimit;
               for (int j = i - 1; j >= firstNonAssociated; j--) {
                  if (photos[j].Time < limit) break;
                  photos[j].AssociatedTrack = prev.AssociatedTrack;
                  photos[j].Lat = prev.AssociatedTrack.EndLat;
                  photos[j].Lon = prev.AssociatedTrack.EndLon;
               }
               firstNonAssociated = -1;
            }
         }
      }

      private static void setLatLon (NameAndDate photo, TrackPoint pt, TrackPoint ptPrev) {
         double legSecs = (pt.Time - ptPrev.Time).TotalSeconds;
         if (legSecs < 0) return;
         if (legSecs == 0) {
            photo.Lat = pt.Lat;
            photo.Lon = pt.Lon;
            photo.AssociatedTrack = pt.Track;
            return;
         }

         double photoSecs = (photo.Time - ptPrev.Time).TotalSeconds; //PW nakijken
         if (legSecs <= 7200) {
            double f = photoSecs / legSecs;
            photo.Lat = ptPrev.Lat + f * (pt.Lat - ptPrev.Lat);
            photo.Lon = ptPrev.Lon + f * (pt.Lon - ptPrev.Lon);
            photo.AssociatedTrack = ptPrev.Track;
            return;
         }
      }

      private List<TrackPoint> loadTrackPoints (ESSearchRequest req) {
         var ret = new List<TrackPoint> (1000000);
         req.Fields = "trackdata";
         req.SetSource ("meta", null);
         req.Sort.Add (new ESSortField ("sort_date", ESSortDirection.asc));

         var bq = new ESBoolQuery ();
         bq.AddFilter (new ESTermQuery ("type", "meta"));
         bq.AddFilter (new ESTermQuery ("user", "peter"));
         bq.AddFilter (new ESTermQuery ("meta.type", "track"));
         req.Query = bq;

         using (var e = new ESRecordEnum (req)) {
            foreach (var d in e) {
               var src = d._Source;
               var meta = src.ReadObj ("meta");
               var data = (JsonArrayValue)d._Fields["trackdata"];
               src["trackdata"] = JsonObjectValue.Parse (data[0]);
               var track = new Track (_GeoNamesMode.Disabled, null);
               track.LoadFromJson (src);
               var ourTrack = new TrackAdmin (d.Id, track);
               foreach (var item in track.Items) {
                  ret.Add (new TrackPoint (item, ourTrack));
               }
            }
         }
         return ret;
      }
      private List<NameAndDate> loadTrackPhotos (ESSearchRequest req) {
         var list = new List<NameAndDate> ();

         var bq = new ESBoolQuery ();
         bq.AddFilter (new ESTermQuery ("type", "photo"));
         bq.AddFilter (new ESTermQuery ("user", "peter"));
         req.Query = bq;
         req.Fields = null;
         req.SetSource (null, null);
         req.Sort.Add (new ESSortField ("photo_date_taken", ESSortDirection.asc));
         using (var e = new ESRecordEnum (req)) {
            foreach (var d in e) {
               var fn = d.ReadStr ("partial_filename").Split ('/')[^1];
               var dt = d.ReadDate ("photo_date_taken");
               list.Add (new NameAndDate (fn, dt));
            }
         }
         return list;
      }

   }
}
