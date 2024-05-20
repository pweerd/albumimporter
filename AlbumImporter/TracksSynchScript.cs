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

//using Bitmanager.Elastic;
//using Bitmanager.Core;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Bitmanager.ImportPipeline;
//using System.Xml;
//using Bitmanager.Xml;
//using Bitmanager.IO;
//using System.Diagnostics;
//using Bitmanager.Gps;
//using Bitmanager.Json;

//namespace AlbumImporter {
//   public class MutableInteger {
//      public int Value;
//      public MutableInteger () { }
//      public MutableInteger (int value) { Value = value; }
//   }


//   public class TracksSynchScript {

//      public object OnDatasourceStart (PipelineContext ctx, object value) {
//         var node = ctx.DatasourceAdmin.ContextNode;
//         string urlTracks = node.ReadStr ("@url_tracks");
//         string urlAlbum = node.ReadStr ("@url_album");

//         List<NameAndDate> trackPhotos, albumPhotos;
//         Dictionary<string, MutableInteger> tracksCounters, albumCounters;

//         var tracksConnection = new ESConnection (urlTracks);
//         var items = loadTracks (tracksConnection);
//         ctx.ImportLog.Log ("Loaded {0} track items", items.Count);
//         for (int ii = 1; ii < items.Count; ii++) {
//            if (items[ii].Time < items[ii - 1].Time)
//               ctx.ImportLog.Log ("Loaded {0} tLeap in time at ix={0}: {1} -> {2}", ii, items[ii - 1].Time, items[ii].Time);
//         }

//         trackPhotos = loadTrackPhotos (tracksConnection, out tracksCounters);
//         albumPhotos = loadAlbumPhotos (new ESConnection (urlAlbum), out albumCounters);
//         albumPhotos.Sort (NameAndDate.CbSortDate);
//         determineLatLon (items, albumPhotos);
//         //propagateLatLonToSameAlbum (albumPhotos);


//         trackPhotos.Sort (NameAndDate.CbSortName);
//         albumPhotos.Sort (NameAndDate.CbSortName);

//         var wtrMissingFromAlbum = IOUtils.CreateOutputStream (@"z:\missingFromAlbum.txt").CreateTextWriter ();
//         var wtrMissingFromTracks = IOUtils.CreateOutputStream (@"z:\missingFromTracks.txt").CreateTextWriter ();


//         int N = trackPhotos.Count;
//         int M = albumPhotos.Count;
//         int i = 0, j = 0;
//         int common = 0;
//         while (i < N && j < M) {
//            //if (trackPhotos[i].Name == "20230930 094326 lnp oisterwijk-goirle")
//            //   Debugger.Break ();
//            int rc = NameAndDate.strComparer.Compare (trackPhotos[i].Name, albumPhotos[j].Name);
//            if (rc == 0) {
//               //albumPhotos[j].Lat = trackPhotos[i].Lat;
//               //albumPhotos[j].Lon = trackPhotos[i].Lon;
//               ++i;
//               ++j;
//               ++common;
//               continue;
//            }
//            if (rc < 0) {
//               wtrMissingFromAlbum.WriteLine (trackPhotos[i].Name);
//               ++i;
//               continue;
//            }
//            wtrMissingFromTracks.WriteLine (albumPhotos[j].Name);
//            ++j;
//            continue;
//         }

//         for (; i < N; i++) wtrMissingFromAlbum.WriteLine (trackPhotos[i].Name);
//         for (; j<M; j++) wtrMissingFromTracks.WriteLine (albumPhotos[j].Name);
//         wtrMissingFromTracks.Close ();
//         wtrMissingFromAlbum.Close ();

//         trackPhotos.Sort (NameAndDate.CbSortDate);
//         albumPhotos.Sort (NameAndDate.CbSortDate);
//         using (var fs = IOUtils.CreateOutputStream (@"Z:\trackphotos.txt")) {
//            var wtr = fs.CreateTextWriter ();
//            foreach (var s in trackPhotos) {
//               wtr.WriteLine (s);
//            }
//            wtr.Flush ();
//         }
//         using (var fs = IOUtils.CreateOutputStream (@"Z:\albumphotos.txt")) {
//            var wtr = fs.CreateTextWriter ();
//            foreach (var s in albumPhotos) {
//               wtr.WriteLine (s);
//            }
//            wtr.Flush ();
//         }


//         ctx.ImportLog.Log ("Tracks={0}, album={1}, common={2}", N, M, common);
//         ctx.ImportLog.Log ("Days: tracks={0}, album={1}", tracksCounters.Count, albumCounters.Count);
//         return null;
//      }


//      void propagateLatLonToSameAlbum (List<NameAndDate> photos) {
//         string album = null;
//         for (int i=0; i<photos.Count; i++) {
//            if (!double.IsNaN (photos[i].Lon)) {
//               album = photos[i].Album;
//               if (string.IsNullOrEmpty (album)) continue;
//               int j;
//               for (j = i - 1; j >= 0; j--) {
//                  if (album != photos[j].Album) break;
//                  if (!double.IsNaN (photos[j].Lon)) break;
//                  photos[j].Lat = photos[i].Lat;
//                  photos[j].Lon = photos[i].Lon;
//               }
//               for (j = i + 1; j <photos.Count; j++) {
//                  if (album != photos[j].Album) break;
//                  if (!double.IsNaN (photos[j].Lon)) break;
//                  photos[j].Lat = photos[i].Lat;
//                  photos[j].Lon = photos[i].Lon;
//               }
//               i = j - 1;
//               continue;
//            }
//         }
//      }
//      void determineLatLon (List<TrackPoint> points, List<NameAndDate> photos) {
//         int N = points.Count;
//         int M = photos.Count;
//         if (N == 0 || M == 0) return;

//         int i = 1, j = 0, k=-1;
//         for (j=0; j<M; j++) {
//            if (photos[j].Name.StartsWith("20100417")) {
//            //if (photos[j].Name == "20100417_092243 Ligfietsen Medemblik(O)") {
//                  k = j;
//               break;
//            }
//         }
//         j = 0;
//         while (i < N && j < M) {
//            if (j == k) { Debugger.Break (); k = -1; }
//            if (points[i].Time < photos[j].Time) {
//               ++i;
//               continue;
//            }
//            if (points[i-1].Time > photos[j].Time) {
//               ++j;
//               continue;
//            }
//            setLatLon (photos[j], points[i], points[i - 1]);
//            ++j;
//         }
//      }

//      private static void setLatLon (NameAndDate photo, TrackPoint curPt, TrackPoint prevPt) {
//         double legSecs = (curPt.Time - prevPt.Time).TotalSeconds;
//         if (legSecs < 0) return;
//         if (legSecs==0) {
//            photo.Lat = curPt.Lat;
//            photo.Lon = curPt.Lon;
//            photo.AssociatedTrack = curPt.Id;
//            return;
//         }

//         double photoSecs = (photo.Time - prevPt.Time).TotalSeconds;
//         if (legSecs <= 7200) {
//            double f = photoSecs / legSecs;
//            photo.Lat = prevPt.Lat + f * (curPt.Lat - prevPt.Lat);
//            photo.Lon = prevPt.Lon + f * (curPt.Lon - prevPt.Lon);
//            photo.AssociatedTrack = prevPt.Id;
//            return;
//         }
//         return;

//         if (photoSecs < 7200) {
//            photo.Lat = prevPt.Lat;
//            photo.Lon = prevPt.Lon;
//            photo.AssociatedTrack = prevPt.Id;
//         } else if (legSecs - photoSecs <= 7200) {
//            photo.Lat = curPt.Lat;
//            photo.Lon = curPt.Lon;
//            photo.AssociatedTrack = curPt.Id;
//         }
//      }

//      private List<TrackPoint> loadTracks(ESConnection c) {
//         var ret = new List<TrackPoint> (1000000);
//         var req = c.CreateSearchRequest ("tracks");
//         req.Fields = "trackdata";
//         req.SetSource ("meta", null);
//         req.Sort.Add (new ESSortField ("sort_date", ESSortDirection.asc));

//         var bq = new ESBoolQuery ();
//         bq.AddFilter (new ESTermQuery ("type", "meta"));
//         bq.AddFilter (new ESTermQuery ("user", "peter"));
//         bq.AddFilter (new ESTermQuery ("meta.type", "track"));
//         req.Query = bq;

//         var e = new ESRecordEnum (req);
//         foreach (var d in e) {
//            var src = d._Source;
//            var meta = src.ReadObj ("meta");
//            var data = (JsonArrayValue)d._Fields["trackdata"];
//            src["trackdata"] = JsonObjectValue.Parse (data[0]);
//            var track = new Track (_GeoNamesMode.Disabled, null);
//            track.LoadFromJson(src);
//            foreach (var item in track.Items) {
//               ret.Add (new TrackPoint (item, d.Id));
//            }
//         }
//         return ret;
//      }
//      private List<NameAndDate> loadTrackPhotos (ESConnection c, out Dictionary<string, MutableInteger> _dayDict) {
//         var list = new List<NameAndDate> ();
//         var dayDict = new Dictionary<string, MutableInteger> ();
//         var req = c.CreateSearchRequest ("tracks");

//         var bq = new ESBoolQuery ();
//         bq.AddFilter (new ESTermQuery ("type", "photo"));
//         bq.AddFilter (new ESTermQuery ("user", "peter"));
//         req.Query = bq;
//         var e = new ESRecordEnum (req);
//         foreach (var d in e) {
//            var fn = d.ReadStr ("partial_filename").Split ('/')[^1];
//            var dt = d.ReadDate ("photo_date_taken");
//            list.Add (new NameAndDate (fn, dt));
//            var day = dt.ToString ("yyyyMMdd");
//            if (dayDict.TryGetValue (day, out var mi))
//               mi.Value++;
//            else
//               dayDict.Add (day, new MutableInteger (1));
//         }
//         _dayDict = dayDict;
//         return list;
//      }

//      private List<NameAndDate> loadAlbumPhotos (ESConnection c, out Dictionary<string, MutableInteger> _dayDict) {
//         var list = new List<NameAndDate> ();
//         var dayDict = new Dictionary<string, MutableInteger> ();
//         var req = c.CreateSearchRequest ("album");

//         var bq = new ESBoolQuery ();
//         bq.AddNot (new ESMatchQuery ("type", "admin_"));
//         bq.AddNot (new ESMatchQuery ("camera", "canoscan 9000f"));
//         bq.AddNot (new ESMatchQuery ("camera", "scanner"));
//         bq.AddFilter (new ESExistsQuery ("camera"));
//         //bq.AddFilter (new ESTermQuery ("year", "2010"));
//         //bq.AddFilter (new ESTermQuery ("month", "04"));
//         //bq.AddFilter (new ESTermQuery ("day", "17"));
//         req.Query = bq;
//         var e = new ESRecordEnum (req);
//         foreach (var d in e) {
//            var dt = d.ReadDate ("date", DateTime.MinValue);
//            if (dt == DateTime.MinValue) continue;

//            var fn = d.Id.Split ('/')[^1];
//            NameAndDate item;
//            list.Add (item=new NameAndDate (fn, dt));

//            item.Album = d.ReadStr ("album", null);
//            var day = d.ReadDate ("date", DateTime.MinValue).ToString ("yyyyMMdd");
//            if (dayDict.TryGetValue (day, out var mi))
//               mi.Value++;
//            else
//               dayDict.Add (day, new MutableInteger (1));
//         }
//         _dayDict = dayDict;
//         return list;
//      }
//   }
//}
