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

using Bitmanager.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AlbumImporter {

   public enum _HideStatus { None, External, Always }
   [Flags]
   public enum _Inherit { None=0, FromDefault=1, Allow=2 }

   //   <root inherit = "true" >
   //     <album minlen="2" force="" take_from_dir="true"/>
   //     <date force = "" type="utc|local" />
   //     <camera force = "scanner" />
   //   </ root >
   public class DirectorySettings {
      public static readonly DirectorySettings Default = new DirectorySettings();
      private static readonly string[] EMPTY_NAMES= new string[0];
      public readonly _Inherit Inherit;
      public readonly _HideStatus HideStatus;
      public readonly string ForcedAlbum;
      public readonly string ForcedUser;
      public readonly string ForcedCamera;
      public readonly int MinAlbumLen;
      public readonly DateTime ForcedDate;
      public readonly DateTimeKind ForcedDateKind;
      public readonly bool UseAlbumFromDir;
      public readonly bool UseDateFromDir;
      public readonly bool UseFileOrder;
      public readonly int SkipDirNames;
      public readonly string[] Names;
      public readonly int TotalNamesLength;

      public DirectorySettings () {
         Inherit = _Inherit.Allow;
         MinAlbumLen = 3;
         Names = EMPTY_NAMES;
         SkipDirNames = -1;
      }
      public DirectorySettings (DirectorySettings other, _HideStatus hideStatus) {
         HideStatus = hideStatus;
         Inherit = other.Inherit;
         ForcedAlbum = other.ForcedAlbum;
         ForcedCamera = other.ForcedCamera;
         ForcedUser = other.ForcedUser;
         MinAlbumLen = other.MinAlbumLen;
         ForcedDate = other.ForcedDate;
         ForcedDateKind = other.ForcedDateKind;
         UseAlbumFromDir = other.UseAlbumFromDir;
         UseDateFromDir = other.UseDateFromDir;
         UseFileOrder = other.UseFileOrder;
         Names = other.Names;
         TotalNamesLength = other.TotalNamesLength;
         SkipDirNames = other.SkipDirNames;
      }

      public DirectorySettings (string name, DirectorySettings def) : this (def, def.HideStatus) {
         if (SkipDirNames < 0) 
            Names = createNames (name, def.Names, out TotalNamesLength);
         else 
            Names = createNames (name, out TotalNamesLength);
      }

      private static string[] createNames (string name, out int len) {
         len = 1 + name.Length;
         var ret = new string[1];
         ret[0] = name;
         return ret;
      }
      private static string[] createNames (string name, string[] existing, out int len) {
         var ret = new string[1 + existing.Length];
         len = 1 + name.Length + existing.Length;
         int i;
         for (i = 0; i < existing.Length; i++) {
            len += existing[i].Length;
            ret[i] = existing[i];
         }
         ret[i] = name;
         return ret;
      }

      public DirectorySettings (XmlHelper xml, DirectorySettings def) : this (xml.DocumentElement, def) {
         string name = Path.GetFileName (Path.GetDirectoryName (xml.FileName));
         if (SkipDirNames < 0)
            Names = createNames (name, def.Names, out TotalNamesLength);
         else if (SkipDirNames == 0)
            Names = createNames (name, out TotalNamesLength);
      }

      public DirectorySettings (XmlNode node, DirectorySettings def) {
         if (def == null || (def.Inherit & _Inherit.Allow) == 0) def = Default;
         Inherit = node.ReadEnum ("@inherit", def.Inherit);
         HideStatus = node.ReadEnum ("@hide", def.HideStatus);
         ForcedUser = node.ReadStr ("@user", def.ForcedUser);
         UseFileOrder = node.ReadBool ("@file_order", UseFileOrder);

         XmlNode x = node.SelectSingleNode ("album");
         ForcedAlbum = x.ReadStr ("@force", def.ForcedAlbum);
         UseAlbumFromDir = x.ReadBool ("@take_album_from_dir", def.UseAlbumFromDir);
         UseDateFromDir = x.ReadBool ("@take_date_from_dir", UseAlbumFromDir);
         MinAlbumLen = x.ReadInt ("@minlen", def.MinAlbumLen);

         x = node.SelectSingleNode ("date");
         ForcedDate = x.ReadDate ("@force", def.ForcedDate);
         ForcedDateKind = x.ReadEnum ("@type", def.ForcedDateKind);

         ForcedCamera = node.ReadStr ("camera/@force", def.ForcedCamera);

         SkipDirNames = node.ReadInt ("@skip_dir_names", def.SkipDirNames - 1);
         Names = EMPTY_NAMES;
      }
   }
}
