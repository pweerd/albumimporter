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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AlbumImporter
{
    public class FileMetaExtractor: BaseMetaExtractor {
      private readonly DirectoryMetaExtractor dirExtractor = new DirectoryMetaExtractor();

      public string Extra;
      private DateTime _date;
      private string _album;
      public DateTime Date => _date == DateTime.MinValue ? dirExtractor.Date : _date;
      public string Album => _album == null ? dirExtractor.Album : _album;

      public int Order;
      private int fileOrder;
      public int FileOrder => fileOrder;
      public _HideStatus HideStatus;


      private static readonly Regex exprOrderedImg = new Regex (@"^(IMG|SAM|P|DSCF|SGDN|Afbeelding)[_\- ]*(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
      private static readonly Regex exprWhatsappImg = new Regex (@"^IMG[_\- ](\d{8})[_\- ]WA(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
      private static readonly Regex exprImgPrefix = new Regex (@"^(IMG|SAM|P|DSCF|Afbeelding)[_\- ]*([^\d*])", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
      private static readonly Regex exprExtra = new Regex (@"\(\p{L}{1,2}\)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
      private static readonly Regex exprOrder = new Regex (@"[^\d]_(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

      public string StrDate => ToStrDate (Date);

      private string setAlbum (DirectorySettings dirSettings, string album) {
         if (album == null || dirSettings.UseAlbumFromDir) {
            album = dirExtractor.Album;
         }
         if (album != null) {
            int ix = album.IndexOf (',');
            if (ix >= 0) album = album.Substring (0, ix).Trim();
         }
         if (album?.Length < dirSettings.MinAlbumLen || onlyDigits (album)) album = null;
         _album = album;
         return album;
      }
      private void setDate (DirectorySettings dirSettings, DateTime dt) {
         if (dt != DateTime.MinValue && !dirSettings.UseDateFromDir) {
            _date = dt;
         }
      }

      public void Extract (DirectorySettings dirSettings, string fullName, DateTime fileDate) {
         //Logs.DebugLog.Log ("Dirsettings UseDateFromDir={0}, UseAlbumFromDir={1}, dirdate={2}", dirSettings.UseDateFromDir, dirSettings.UseAlbumFromDir, dirExtractor.Date);
         _album = null; Extra = null; _date = DateTime.MinValue; Order = 0;

         if (!dirSettings.UseFileOrder) fileOrder = 0; //Always reset order if we don't use it
         dirExtractor.Extract (dirSettings);

         HideStatus = dirSettings.HideStatus;
         string name = Path.GetFileNameWithoutExtension (fullName);
         if (HideStatus == _HideStatus.None && name.EndsWith ("_")) HideStatus = _HideStatus.External;
         name = name.Trim(seps);
         Match match;

         //Interpret names like IMG-20230709-WA0006 (whatsapp)
         match = exprWhatsappImg.Match (name);
         if (match.Success) {
            setAlbum(dirSettings, "WhatsApp");
            Order = Invariant.ToInt32 (match.Groups[2].Value);
            setDate(dirSettings, fileDate);

            int ty =0, tm=0, td=0;
            if (tryParseDate (Invariant.ToInt32 (match.Groups[2].Value), ref ty, ref tm, ref td)) {
               if (fileDate.Year != ty || fileDate.Month!=tm || fileDate.Day !=td) {
                  setDate (dirSettings, new DateTime (ty, tm, td, 0, 0, 0, DateTimeKind.Local));
               }
            }
            return;
         }

         //Interpret names like img0001.jpg
         match = exprOrderedImg.Match (name);
         if (match.Success) {
            Order = Invariant.ToInt32 (match.Groups[2].Value);
            name = name.Substring (match.Length).Trim (seps);
            if (null != setAlbum (dirSettings, name)) return;
            return;
         }

         //Strip and extract numbers at end and interpret them as order
         match = exprOrder.Match (name);
         if (match.Success) {
            Order = Invariant.ToInt32 (match.Groups[1].Value);
            name = name.Substring (0, match.Index+1).Trim (seps);
         }

         //Strip and extract things like (P)
         match = exprExtra.Match (name);
         if (match.Success) {
            Extra = name.Substring (match.Index + 1, match.Length - 2);
            name = name.Substring (0, match.Index).Trim (seps);
         }


         //Strip prefixing blocks of numbers-only, possibly interpret them as date
         long num = 0;
         long lastNum = 0;
         long firstNum = -1;
         int lastNumDigits=0;
         int numDigits=0;
         int lastIx = -1;

         int i;
         for (i = 0; i < name.Length; i++) {
            int ch = name[i];
            if (ch >= '0' && ch <= '9') {
               ++numDigits;
               num = num * 10 + ch - '0';
               if (lastIx < 0) lastIx = i;
               continue;
            }
            if (ch == ' ' || ch == '-' || ch == '_') {
               lastNumDigits = numDigits;
               lastNum = num;
               if (firstNum < 0) firstNum = num;
               lastIx = -1;
               continue;
            }
            if (lastIx < 0) lastIx = i;
            break;
         }
         if (i>=name.Length) {
            lastNumDigits = numDigits;
            lastNum = num;
            lastIx = i;
         }
         if (firstNum < 0) firstNum = num;

         name = name.Substring (lastIx);
         //switch (name.ToLowerInvariant()) {
         //   case "p":
         //   case "sam":
         //   case "img":
         //      name = null;
         //      break;
         //}
         setAlbum (dirSettings, name);

         bool parsed = false;

         int y = dirExtractor.yy, m = dirExtractor.mm, d = dirExtractor.dd, hh = -1, min = -1, sec = -1;

         switch (lastNumDigits) {
            case 4: //only year
               parsed = tryParseYearOrMMDD (lastNum, ref y, ref m, ref d);
               break;
            case 6: //only time
               parsed = tryParseTime (lastNum, ref hh, ref min, ref sec);
               break;
            case 8: //only date
               parsed = tryParseDate (lastNum, ref y, ref m, ref d);
               break;
            case 14: //date plus time
               if (tryParseTime (lastNum, ref hh, ref min, ref sec))
                  parsed = tryParseDate (lastNum / 1000000, ref y, ref m, ref d);
               break;
            case 10: //date as mmdd plus time
               if (dirExtractor.yy > 0) {
                  if (tryParseTime (lastNum, ref hh, ref min, ref sec)) {
                     parsed = true;
                     long dateNum = lastNum / 1000000 + y * 10000;
                  }
               }
               break;
         }

         if (isValidDate (y, m, d)) {
            try {
               setDate (dirSettings, 
                  isValidTime (hh, min, sec) ? new DateTime (y, m, d, hh, min, sec, dirSettings.ForcedDateKind)
                                             : new DateTime (y, m, d, 0, 0, 0, dirSettings.ForcedDateKind));
            } catch {
               parsed = false;
            }
         }

         if (dirSettings.UseFileOrder)
            Order = ++fileOrder;
         else if (!parsed) 
            Order = (int)firstNum;
      }
   }
}
