/* Copyright © 2023, De Bitmanager
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AlbumImporter;

namespace UnitTests {
   [TestClass]
   public class FileTests {
      private readonly FileMetaExtractor extractor = new FileMetaExtractor ();
      private readonly DateTime testDate = new DateTime (2023, 2, 1, 12, 3, 4, DateTimeKind.Local);

      [TestMethod]
      public void TestExtract () {
         check (null, null, 1, null, "img001.jpg");
         check (null, null, 1, null, "img-001.jpg");
         check (null, null, 1, null, "sam-001.jpg");
         check (null, null, 1, null, "p-001.jpg");
         check (null, null, 195, null, "Afbeelding 195.jpg");
         check ("WhatsApp", "2023-02-01", 6, null, "IMG-20230709-WA0006.jpg");
         check ("BFYV6816", null, 0, null, "BFYV6816.jpg");
         check ("BFYV6816", "2022-08-14", 0, null, "20220814_214413 BFYV6816.jpg");
         check ("Familiedag 2023", "2023-06-17", 0, "D", "20230617_120214 Familiedag 2023(D)");
         check ("1e van 29 mei 2000", null, 137, null, "137 1e van 29 mei 2000.jpg");
         check ("wilma", "2022-08-14", 0, null, @"wilma\20220814_214413.jpg");
         check (null, "2023-04-01", 570, null, @"2023\04\SGDN0570.JPG");
         check ("aap", "2022-08-14", 23, "P", @"wilma\20220814_214413 aap(P)_23.jpg");

         check ("fotos", null, 1693, null, @"D\dakkapel\fotos\IMG_1693.JPG");
         check ("fotos", null, 1693, null, @"D\dakkapel\fotos\IMG_1693_1.JPG");
         check ("Zeiltocht Thijs", "1995-01-01", 2, null, @"D:\Fotos\Medialab\1995 - Zeiltocht Thijs\002.jpg");
         check ("Zeiltocht Thijs", "1995-01-01", 0, null, @"D:\Fotos\Medialab\1995 - Zeiltocht Thijs.jpg");
         check ("Zeiltocht Thijs", "1995-02-03", 0, null, @"D:\Fotos\Medialab\1995-02-03 - Zeiltocht Thijs.jpg");
         check ("Zeiltocht Thijs", "1995-02-03", 0, null, @"D:\Fotos\Medialab\1995\02-03 - Zeiltocht Thijs.jpg");
      }

      private DirectorySettings getSettings(string name) {
         var c = new DirectorySettingsCache(null);
         return c.GetSettings(Path.GetDirectoryName(name), 0);
      } 
      private void check (string album, string date, int order, string extra, string name) {

         extractor.Extract (getSettings(name), name, testDate);
         Assert.AreEqual (date, extractor.StrDate);
         Assert.AreEqual (album, extractor.Album);
         Assert.AreEqual (extra, extractor.Extra);
         Assert.AreEqual (order, extractor.Order);
      }
      private void check (string album, string date, string name, bool needDate=true) {
         extractor.Extract (getSettings (name), name, testDate);
         Assert.AreEqual (date, extractor.StrDate);
         Assert.AreEqual (album, extractor.Album);
         Assert.AreEqual (null, extractor.Extra);
         Assert.AreEqual (0, extractor.Order);
      }

   }
}
