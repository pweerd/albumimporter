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
using Bitmanager.ImportPipeline;
using Bitmanager.Xml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace AlbumImporter {
   public class IdInfo {
      public readonly string Id;
      public readonly string User;
      public readonly string FileName;
      public readonly DateTime DateUtc;

      public IdInfo (GenericDocument doc, RootReplacer repl) {
         var src = doc._Source;
         Id = src.ReadStr ("id");
         FileName = repl.GetRealFileName (Id);
         User = src.ReadStr ("user");
         DateUtc = src.ReadDate ("date");
      }
      public IdInfo (string id, string user, string file) {
         Id = id;
         FileName = file;
         User = user;
         DateUtc = File.GetLastWriteTimeUtc (file);
      }

      public override string ToString () {
         return FileName;
      }
   }

   public class IDDatasource : Datasource {
      private RootReplacer rootReplacer;
      private Regex filter;
      private string fixedFile;
      private string url;
      private int bufferSize;
      private int sleepTime;
      public void Import (PipelineContext ctx, IDatasourceSink sink) {
         var p = ctx.Pipeline;
         var repl = rootReplacer;
         if (fixedFile != null) {
            p.HandleValue (ctx, "record", new IdInfo("dummy\\"+Path.GetFileName(fixedFile), "dummy", fixedFile));
            return;
         }
         var req = Utils.CreateESRequest (url);
         req.Sort.Add (new ESSortField ("id.keyword", ESSortDirection.asc));

         var e = new ESRecordEnum (req, bufferSize, "15m");
         e.Async = true;
         foreach (var rec in e) {
            var idInfo = new IdInfo (rec, repl);
            if (filter != null && !filter.IsMatch (idInfo.Id)) continue; 
            ctx.IncrementEmitted ();
            p.HandleValue (ctx, "record", idInfo);
            if (sleepTime>0) Thread.Sleep (sleepTime);
         }
      }

      public void Init (PipelineContext ctx, XmlNode node) {
         rootReplacer = Utils.CreateRootReplacer (node);
         fixedFile = node.ReadStr ("id/@file", null);
         if (fixedFile != null) return;

         url = node.ReadStr ("@url");
         bufferSize = node.ReadInt ("@buffersize", ESRecordEnum.DEF_BUFFER_SIZE);
         sleepTime = node.ReadInt ("@sleep_per_record", 0);
         string f = node.ReadStr ("@filter", null);
         if (f != null) filter = new Regex (f, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
      }
   }
}
