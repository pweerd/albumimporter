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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlbumImporter {
   public class DirectoryMetaExtractor: BaseMetaExtractor {
      public string Album;
      public DateTime Date;
      public string StrDate;
      public int yy, mm, dd;

      private DirectorySettings prevDir;

      public void Extract (DirectorySettings dir) {
         if (prevDir == dir) return;
         prevDir = dir;

         yy = 0; mm = 0; dd = 0;
         Album = null;
         Date = DateTime.MinValue;
         string[] arr = dir.Names;
         for (int i = 0; i<arr.Length; i++) {
            var s = arr[i];
            if (i == 0 && s.Length >= 2 && s[1] == ':') continue;
            if (onlyDigits (s)) {
               tryInterpretDigitsAsDate (s, ref yy, ref mm, ref dd);
               continue;
            }

            var parts = split (s);
            if (parts.Length == 2) {
               int p0 = 0, p1 = 0;
               if (tryInterpretDigitsAsYear (parts[0], ref p0) && tryInterpretDigitsAsYear (parts[1], ref p1)) {
                  yy = p0;
                  Album = parts[0] + "-" + parts[1];
                  continue;
               }
            }

            string album = null;
            for (int j=0; j<parts.Length; j++) {
               string part = parts[j];
               if (onlyDigits (part)) {
                  tryInterpretDigitsAsDate (part, ref yy, ref mm, ref dd);
                  continue;
               }

               if (album == null) album = part; else album = album + " " + part;
            }
            if (album != null && album.Length>2) Album = album; //PW: Setting
         }

         try {
            if (yy>0) {
               Date = new DateTime (yy, mm <= 0 ? 1 : mm, dd <= 0 ? 1 : dd);
               StrDate = Date.ToString ("yyyy-MM-dd");
            }
         } catch {
         }
      }

      private static readonly char[] dirSeps = new char[] { '\\' };

      private static bool tryInterpretDigitsAsYear (string s, ref int yy) {
         bool ret = false;
         if (s.Length == 4 && onlyDigits (s)) {
            int val = Invariant.ToInt32 (s);
            if (val > 1950 && val < 2200) {
               yy = val;
               ret = true;
            }
         }
         return ret;
      }
      private static bool tryInterpretDigitsAsDate (string s, ref int yy, ref int mm, ref int dd) {
         int y, m, d;
         bool ret = false;
         try {
            if (onlyDigits (s)) {
               int val = Invariant.ToInt32 (s);
               switch (s.Length) {
                  case 2:
                     if (mm <= 0) {
                        if (val > 0 && val <= 12) { mm = val; ret = true; }
                        break;
                     }
                     if (dd <= 0) {
                        if (val > 0 && val <= 31) { dd = val; ret = true; }
                     }
                     break;

                  case 4:
                     if (val > 1950 && val < 2200) {
                        yy = val; mm = 0; dd = 0; ret = true; break;
                     }
                     m = val / 100;
                     d = val % 100;
                     if (m > 0 && m <= 12 && d > 0 && d <= 31 && yy > 0 && mm <= 0) {
                        mm = m; dd = d; ret = true;
                     }
                     break;
                  case 6:
                     y = val / 100;
                     m = val % 100;
                     if (isValidDate(y,m,1)) {
                        yy = y;
                        mm = m;
                        dd = 0;
                        ret = true;
                     }
                     break;
                  case 8:
                     ret = tryParseDate (val, ref yy, ref mm, ref dd);
                     break;
               }
            }
         } catch { }
         return ret;
      }

      private static bool tryInterpretDigitsAsDate (string s, ref int yy, ref int mm) {
         bool ret = false;
         if (yy > 0 && s.Length == 2 && onlyDigits (s)) {
            int val = Invariant.ToInt32 (s);
            if (isValidMonth (val)) { mm = val; ret = true; }
         }
         return ret;
      }
   }
}
