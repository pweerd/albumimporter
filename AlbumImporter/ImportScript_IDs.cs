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
using Bitmanager.Xml;
using Bitmanager.ImportPipeline;
using System.Xml;
using IStreamProvider = Bitmanager.ImportPipeline.StreamProviders.IStreamProvider;
using Bitmanager.Json;

namespace AlbumImporter {
   public class ImportScript_Ids {
      private XmlNode prev = null;
      private string user = null;
      private string root = null;
      public object OnId (PipelineContext ctx, object value) {
         var elt = (IStreamProvider)value;
         if (elt.ContextNode != prev) {
            prev = elt.ContextNode;
            user = prev.ReadStr ("@user");
            root = elt.VirtualRoot;
         }
         var ep = (JsonObjectValue)ctx.Action.Endpoint.GetField (null);
         ep["id"] = elt.VirtualName;
         ep["root"] = root;
         ep["user"] = user;
         ep["date"] = elt.LastModifiedUtc;
         return value;
      }
   }
}
