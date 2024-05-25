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
using Bitmanager.Test;
using AlbumImporter;

namespace UnitTests {
   [TestClass]
   public class DirectoryTests: TestBaseSimple {
      private readonly DirectoryMetaExtractor dir = new DirectoryMetaExtractor ();
      [TestMethod]
      public void TestDirectoryExtract () {
         check (null, null, @"");
         check (null, null, @"c:");
         check (null, null, @"c:\");
         check ("aap", null, @"c:\aap");
         check (null, null, @"c:\");

         check ("1993-1994", "1993-01-01", @"c:\1993 - 1994 ");
         check ("geboorte kim", "1993-01-01", @"c\1993 geboorte kim ");
         check ("geboorte kim", "1993-01-01", @"c\1993 geboorte kim \123");
         check ("geboorte kim", "1993-03-01", @"c\1993\03\ geboorte kim");
         check ("geboorte kim", "1993-03-05", @"c\1993\03\05 geboorte kim");
         check ("geboorte kim", "1993-03-05", @"c\1993\0305 geboorte kim");
         check ("geboorte kim", "1994-03-05", @"c\1993\19940305 geboorte kim");
         check ("aap", "1993-01-01", @"c\1993 geboorte kim \aap");
         check (null, "1993-04-01", @"c\1993\04");
         check ("Zeiltocht Thijs", "1995-01-01", @"D:\Fotos\Medialab\1995 - Zeiltocht Thijs");

      }


      //root: skip_dir_names=2
      //lvl1: skip_dir_names=?
      //lvl2: skip_dir_names=?
      //lvl3: skip_dir_names=0
      //lvl4: skip_dir_names=?
      [TestMethod]
      public void TestSettings() {
         string root = base.testAssembly.FindDirectoryToRoot ("data", Bitmanager.IO.FindToTootFlags.Except);
         string deepest = Path.Combine (root, @"lvl1\lvl2\lvl3\lvl4");
         var cache = new DirectorySettingsCache (null);
         var settings4 = cache.GetSettings (deepest, root.Length);
         deepest = Path.GetDirectoryName (deepest);
         var settings3 = cache.GetSettings (deepest, root.Length);
         deepest = Path.GetDirectoryName (deepest);
         var settings2 = cache.GetSettings (deepest, root.Length);
         deepest = Path.GetDirectoryName (deepest);
         var settings1 = cache.GetSettings (deepest, root.Length);
         deepest = Path.GetDirectoryName (deepest);
         var settings0 = cache.GetSettings (deepest, root.Length);

         Assert.AreEqual (0, settings0.Names.Length);
         Assert.AreEqual (0, settings1.Names.Length);
         Assert.AreEqual (1, settings2.Names.Length);
         Assert.AreEqual ("lvl2", settings2.Names[0]);
         Assert.AreEqual (1, settings3.Names.Length);
         Assert.AreEqual ("lvl3", settings3.Names[0]);
         Assert.AreEqual (2, settings4.Names.Length);
         Assert.AreEqual ("lvl3", settings4.Names[0]);
         Assert.AreEqual ("lvl4", settings4.Names[1]);

      }

      private void check (string album, string date, string name) {
         var cache = new DirectorySettingsCache (null);

         dir.Extract (cache.GetSettings(name, 3));
         Assert.AreEqual (date, dir.StrDate);
         Assert.AreEqual (album, dir.Album);
      }
   }
}
