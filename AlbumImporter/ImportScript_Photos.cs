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

using Bitmanager.Imaging;
using Bitmanager.ImportPipeline.StreamProviders;
using Bitmanager.ImportPipeline;
using Bitmanager.IO;
using Bitmanager.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Bitmanager.Core;
using System.Drawing.Imaging;
using Bitmanager.Xml;
using Bitmanager.Elastic;
using System.Text.RegularExpressions;
using Bitmanager.IR;
using System.Runtime;

namespace AlbumImporter {
   enum Orientation { None = 0, Rotate_0 = 1, Rotate_90 = 6, Rotate_180 = 3, Rotate_270 = 8 };


   public class ImportScript_Photos: ImportScriptBase {
      const string WHATSAPP = "WhatsApp";
      private static readonly Dictionary<string, int> albumIDs = new Dictionary<string, int> ();
      private readonly FileMetaExtractor extractor = new FileMetaExtractor ();
      private DirectorySettingsCache settingsCache = new DirectorySettingsCache (null);
      private CaptionCollection captions;
      private OcrCollection ocrTexts;
      private TrackPhotoCollection trackPhotos;
      private List<DbFace> faces;
      private string[] faceNames;

      private Lexicon lexicon;
      private Hypernyms hypernyms;
      private HypernymCollector hypernymCollector;
      private RegexTokenizer tokenizer;
      private static readonly string[] monthNames = { 
         "januari", 
         "februari",
         "maart",
         "april",
         "mei",
         "juni",
         "juli",
         "augustus",
         "september",
         "oktober",
         "november",
         "december"
      };

      public object OnDatasourceStart (PipelineContext ctx, object value) {
         const _XmlRawMode mandatory = _XmlRawMode.EmptyToNull | _XmlRawMode.ExceptNullValue;

         Init (ctx, false);
         var ds = (DatasourceAdmin)value;
         var settingsNode = ds.ContextNode.SelectSingleNode ("settings");
         settingsCache = new DirectorySettingsCache (settingsNode == null ? null : new DirectorySettings (settingsNode, null));

         tokenizer = new RegexTokenizer ();
         lexicon = new Lexicon (ctx.ImportEngine.Xml.CombinePath ("dut.lex"));
         hypernyms = new Hypernyms (ctx.ImportEngine.Xml.CombinePath ("hypernyms.txt"), true);
         hypernymCollector = new HypernymCollector (tokenizer, hypernyms, lexicon);

         var dsNode = ds.ContextNode;
         captions = new CaptionCollection (ctx.ImportLog, dsNode.ReadStrRaw ("captions/@url", mandatory));
         ocrTexts = new OcrCollection (ctx.ImportLog, dsNode.ReadStrRaw ("ocr/@url", mandatory));
         trackPhotos = new TrackPhotoCollection (ctx.ImportLog, dsNode.ReadStrRaw ("trackphotos/@url", mandatory));
         faces = new FaceCollection (ctx.ImportLog, dsNode.ReadStrRaw ("faces/@url", mandatory)).GetFaces ();
         faces.Sort ((a, b) => string.Compare (a.Id, b.Id, StringComparison.Ordinal));

         faceNames = ReadFaceNames();

         num_en = 0;
         num_portrait = 0;
         handleExceptions = true;
         return value;
      }

      private int num_en, num_portrait;

      public object OnDatasourceEnd (PipelineContext ctx, object value) {
         handleExceptions = true;
         var ds = (DatasourceAdmin)value;
         ctx.ImportLog.Log ("Number of en captions: {0}, portrait={1}", num_en, num_portrait);
         return value;
      }

      private static bool isWhatsappProp (PropertyItem p) {
         return p.Id == (int)ExifTags.ChrominanceTable || p.Id == (int)ExifTags.LuminanceTable;
      } 
      private static bool isProbablyWhatsapp (ExifProperties props) {
         if (props.List.Length != 2) return false;
         return isWhatsappProp (props.List[0]) && isWhatsappProp (props.List[1]);
      }
      private int findFirstFace (string id) {
         string key = id + "~";
         int i = -1;
         int j = faces.Count;
         while (i+1<j) {
            int m = (i+j)/ 2;
            int rc = string.Compare (faces[m].Id, key, StringComparison.Ordinal);
            if (rc < 0) i = m; else j = m;
         }
         return j < faces.Count && faces[j].Id.StartsWith (key) ? j : faces.Count;
      }


      private static string createLocation(float lat, float lon) {
         return Invariant.Format ("{0:F4},{1:F4}", lat, lon);
      }
      public object OnPhoto (PipelineContext ctx, object value) {

         idInfo = (IdInfo)value;
         var rec = ctx.Action.Endpoint.Record;

         int ix = idInfo.Id.IndexOf ('\\');

         string relName = idInfo.Id.Substring(ix+1);
         rec["_id"] = idInfo.Id;
         rec["file"] = idInfo.Id;
         string captionEN = null;
         string captionNL = null;
         if (captions.TryGetValue (idInfo.Id, out var caption)) {
            if (caption.Caption_EN != null) rec["text_en"] = captionEN = caption.Caption_EN;
            if (caption.Caption_NL != null) rec["text_nl"] = captionNL = caption.Caption_NL;
         }
         if (ocrTexts.TryGetValue (idInfo.Id, out var ocrText)) {
            rec["ocr"] = ocrText.Text;
         }
         rec["ext"] = Path.GetExtension (relName).Substring (1);
         rec["root"] = idInfo.Id.Substring (0, ix-1);

         var fullName = idInfo.FileName;
         var dirName = Path.GetDirectoryName (fullName);
         var rootLen = fullName.Length - relName.Length;
         //ctx.ImportLog.Log ("rel={0}, rootlen={1}, dir={2}, full={3}", elt.RelativeName, rootLen, dirName, fullName);
         DirectorySettings dirSettings = settingsCache.GetSettings (dirName, rootLen);

         var user = dirSettings.ForcedUser;
         if (user == null) user = idInfo.User;
         if (user != null) rec["user"] = user;

         extractor.Extract (dirSettings, relName, idInfo.DateUtc.ToLocalTime ());

         string camera;
         string location = null;
         DateTime date = DateTime.MinValue;
         bool whatsapp;
         using (var fs = IOUtils.CreateInputStream (idInfo.FileName))
         using (var bm = Image.FromStream (fs, false, false)) {
            rec["height"] = bm.Height;
            rec["width"] = bm.Width;
            var props = new ExifProperties (bm);
            whatsapp = isProbablyWhatsapp (props);
            PropertyItem p;
            if (!dirSettings.UseDateFromDir) {
               p = props[ExifTags.DateTimeOriginal];
               if (p == null) p = props[ExifTags.DateTimeDigitized];
               try {
                  if (p != null) {
                     date = p.AsDateTime ().ToLocalTimeAssumeLocalIfUns ();
                  }
               } catch { }
            }
            p = props[ExifTags.Model];
            camera = (p != null) ? p.AsString () : dirSettings.ForcedCamera;
            if (camera != null) rec["camera"] = camera;


            PropertyItem latValue, latRef, lonValue, lonRef;
            latValue = props[ExifTags.GPSLatitude];
            if (latValue != null) {
               latRef = props[ExifTags.GPSLatitudeRef];
               if (latRef != null) {
                  lonValue = props[ExifTags.GPSLongitude];
                  if (lonValue != null) {
                     lonRef = props[ExifTags.GPSLongitudeRef];
                     if (lonRef != null) {
                        try {
                           location = createLocation ((float)latValue.AsGpsCoord (latRef), (float)lonValue.AsGpsCoord (lonRef));
                        } catch { }
                     }
                  }
               }
               p = props[ExifTags.GPSAltitude];
               try {
                  if (p != null) rec["ele"] = p.AsDouble ();
               } catch { }
            }

            //Fetch orientation
            try {
               p = props[ExifTags.Orientation];
               rec["orientation"] = p == null ? 0 : (int)p.AsLong ();
            } catch { }

         }

         //Sync with trackphoto's and propagate location trackid and timezone
         if (trackPhotos.TryGetValue(relName, out var trackPhoto)) {
            if (location == null) location = createLocation (trackPhoto.Lat, trackPhoto.Lon);
            if (trackPhoto.Timezone != null) rec["tz"] = trackPhoto.Timezone;
            rec["trkid"] = trackPhoto.TrackId;
         }
         if (location != null) rec["location"] = location;

         string album = dirSettings.ForcedAlbum != null ? dirSettings.ForcedAlbum : extractor.Album;
         if (album != null && album.Length < dirSettings.MinAlbumLen) album = null;
         if (dirSettings.ForcedDate != DateTime.MinValue) date = dirSettings.ForcedDate;
         if (date == DateTime.MinValue) date = extractor.Date;

         //Tag whatsapp images and optional replace the album name
         if (whatsapp) {
            album = replaceAlbumForWhatsapp (relName, album);
            date = replaceDateForWhatsapp (date);
            if (camera == null) {
               rec["camera"] = camera = WHATSAPP;
            }
         }


         int y, m, d;
         if (date == DateTime.MinValue) {
            rec["sort_key"] = getAlbumId(album) * YEAR_MULTIPLIER + extractor.Order;
            rec["yyyymmdd"] = string.Empty;
            y = 0;
            m = 0;
            d = 0;
         } else {
            DateTime dtLocal = date.ToLocalTimeAssumeLocalIfUns ();
            DateTime dtUtc = date.ToUniversalTimeAssumeLocalIfUns ();
            rec["sort_key"] = toSortKey (dtUtc, extractor.Order);
            rec["date"] = dtUtc;
            rec["yyyymmdd"] = dtLocal.ToString ("yyyy-MM-dd");
            y = dtLocal.Year;
            m = dtLocal.Month;
            d = dtLocal.Day;
         }
         rec["year"] = y;
         rec["month"] = m;
         rec["day"] = d;

         var sb = new StringBuilder ();
         sb.Append ('[').Append (y).Append (']');


         if (album != null) {
            sb.Append (' ').Append (album);
         }
         rec["album"] = sb.ToString ();

         int dirLen = Path.GetDirectoryName (idInfo.Id).Length;
         int relnameOffset = 1+dirLen - dirSettings.TotalNamesLength;
         rec["relname_offset"] = relnameOffset;
         //ctx.ImportLog.Log ("dirlen={0}, offs={1}, f={2}, totlen={3}, name={4}", dirLen, relnameOffset, elt.VirtualName, dirSettings.TotalNamesLength, string.Join ("/", dirSettings.Names));
         sb.Append (' ').Append (idInfo.Id.Substring(relnameOffset));
         sb.Append (' ').Append (idInfo.User);

         //Include month and season
         if (m > 0) {
            sb.Append (' ').Append (monthNames[m - 1]);
            List<string> season = new List<string> ();
            switch (m) {
               case 1: season.Add ("winter"); season.Add ("~winter"); break;
               case 2: season.Add ("winter"); season.Add ("~winter"); break;
               case 3: season.Add(d < 21 ? "winter" : "lente"); season.Add ("~winter"); season.Add ("~lente"); break;
               case 4: season.Add ("lente"); season.Add ("~lente"); break;
               case 5: season.Add ("lente"); season.Add ("~lente"); break;
               case 6: season.Add (d < 21 ? "lente" : "zomer"); season.Add ("~zomer"); season.Add ("~lente"); break;
               case 7: season.Add ("zomer"); season.Add ("~zomer"); break;
               case 8: season.Add ("zomer"); season.Add ("~zomer"); break;
               case 9: season.Add (d < 21 ? "zomer" : "herfst"); season.Add ("~zomer"); season.Add ("~herfst"); break;
               case 10: season.Add ("herfst"); season.Add ("~herfst"); break;
               case 11: season.Add ("herfst"); season.Add ("~herfst"); break;
               case 12: season.Add (d < 21 ? "herfst" : "winter"); season.Add ("~herfst"); season.Add ("~winter"); break;
            }
            sb.Append (' ').Append (season[0]);
            rec["season"] = new JsonArrayValue(season);
         }

         //Extra processing for some captions
         if (captionEN != null) {
            ++num_en;
            if (isWordInString (captionEN, "wearing") ||
                isWordInString (captionEN, "wears") ||
                isWordInString (captionEN, "posing") ||
                isWordInString (captionEN, "poses") ||
                isWordInString (captionEN, "pose") ||
                isWordInString (captionEN, "posed")) {
               sb.Append (" portret");
               ++num_portrait;
            }
            if (isWordInString (captionEN, "husband")) sb.Append (" echtgenoot");
            else if (isWordInString (captionEN, "husbands")) sb.Append (" echtgenoten");
            if (isWordInString (captionEN, "wife")) sb.Append (" echtgenote");
            else if (isWordInString (captionEN, "wives")) sb.Append (" echtgenotes");
         }

         //Indicate ocr-status
         if (ocrText != null)
            sb.Append (ocrText.Valid ? " _OCR_ _OCRV_" : " _OCR_");

         var tokens = tokenizer.Tokenize (null, rec.ReadStr ("ocr", null));
         tokens = tokenizer.Tokenize (tokens, captionNL);
         for (int i=0; i<tokens.Count; i++) {
            if (tokens[i].Contains("school")) {
               sb.Append (" school");
               break;
            }
         }
         var hnyms = hypernymCollector.Collect (null, tokens, true);
         tokens.Clear ();
         hnyms = hypernymCollector.Collect (hnyms, tokenizer.Tokenize (tokens, sb.ToString()), false);
         hypernymCollector.ToString (sb, hnyms);

         rec["text"] = sb.ToString ();

         if (extractor.Extra != null) rec["extra"] = extractor.Extra;
         rec["album_len"] = album?.Length;

         switch (extractor.HideStatus) {
            case _HideStatus.External:
               rec["hide"] = "external";
               break;
            case _HideStatus.Always:
               rec["hide"] = "external always";
               break;
         }

         addFaces (rec, idInfo.Id);

         return null;
      }

      /// <summary>
      /// If the album names seems to be more or less the filename, without a date/time prefix,
      /// we replace the album by 'WhatsApp'
      /// </summary>
      private static string replaceAlbumForWhatsapp (string relname, string album) {
         string fn = Path.GetFileNameWithoutExtension (relname);
         int ix = fn.IndexOf (album, StringComparison.InvariantCultureIgnoreCase);
         if (ix <= 0) return WHATSAPP;
         for (int i = 0; i < ix; i++) {
            int ch = fn[i];
            if (ch >= '0' && ch <= '9') continue;
            switch (ch) {
               case ' ':
               case '-':
               case '_': continue;
            }
            return WHATSAPP;
         }
         return album;
      }

      /// <summary>
      /// For photo's coming from whatsapp, we replace the date by the file date
      /// We prefer manual assigned dates when they are specific enough
      /// (to prevent wrong assignment due to editing)
      /// </summary>
      private DateTime replaceDateForWhatsapp (DateTime org) {
         if (org == DateTime.MinValue) goto RET_FILEDATE;
         var local = org.ToLocalTimeAssumeLocalIfUns ();
         if (local.Hour == 0 && local.Minute == 0 && local.Second == 0) {
            var local2 = idInfo.DateUtc.ToLocalTime ();
            if (local2.Year != local.Year) goto RET_ORGDATE;
            if (local2.Month != local.Month) {
               if (local.Month==1 && local.Day==1) goto RET_FILEDATE;
               goto RET_ORGDATE;
            } 
            if (local2.Day == local.Day) goto RET_FILEDATE;
            if (local.Day == 1) goto RET_FILEDATE;
         }
      RET_ORGDATE: return org;

      RET_FILEDATE: return idInfo.DateUtc;
      }


      private void addFaces(JsonObjectValue rec, string id) {
         int i = findFirstFace (id);
         if (i >= faces.Count) goto NO_NAMES;

         var face = faces[i];
         if (face.FaceCount == 0) goto NO_NAMES;
         int end = i + face.FaceCount;
         var arr = new JsonArrayValue ();
         rec["names"] = arr;
         rec["face_count"] = face.FaceCount;

         for (; i<end; i++) {
            if (faces[i].Names.Count == 0) continue;
            var name = faces[i].Names[0];
            name.UpdateName (this.faceNames);
            arr.Add (name.ToJson ());
         }
         return;

      NO_NAMES:
         rec["name_count"] = 0;
      }


      private bool isWordInString (string outer, string arg) {
         int idx = 0;
         while (true) {
            int i = outer.IndexOf (arg, idx); 
            if (i < 0) break;
            idx = i + arg.Length;
            if (i> 0 && char.IsLetter (outer[i-1])) continue;
            if (idx >= outer.Length || !char.IsLetter (outer[idx])) return true;
         }
         return false;
      }

      private int getAlbumId(string album) {
         int albumId=0;
         if (!string.IsNullOrEmpty (album)) {
            if (!albumIDs.TryGetValue (album, out albumId)) {
               albumId = 1 + albumIDs.Count;
               albumIDs[album] = albumId;
            }
         }
         return albumId;
      }


      const long SS_MULTIPLIER = 1000L;
      const long MM_MULTIPLIER = 100 * SS_MULTIPLIER;
      const long HH_MULTIPLIER = 100 * MM_MULTIPLIER;
      const long DAY_MULTIPLIER = 100 * HH_MULTIPLIER;
      const long MONTH_MULTIPLIER = 100 * DAY_MULTIPLIER;
      const long YEAR_MULTIPLIER = 100 * MONTH_MULTIPLIER;

      private static long toSortKey (DateTime date, int order) {
         return
            order
            + date.Year * YEAR_MULTIPLIER
            + date.Month * MONTH_MULTIPLIER
            + date.Day * DAY_MULTIPLIER
            + date.Hour * HH_MULTIPLIER
            + date.Minute * MM_MULTIPLIER
            + date.Second * SS_MULTIPLIER;
      }
      private static string toStr(DateTime dt) {
         return dt.Kind + "_" + dt.ToString ("O");
      }

   }
}
