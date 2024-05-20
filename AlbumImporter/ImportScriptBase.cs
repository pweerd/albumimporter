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

namespace AlbumImporter {
   /// <summary>
   /// Base class for the ImportScripts
   /// </summary>
   public class  ImportScriptBase {
      protected IdInfo idInfo;
      protected Logger logger;
      protected string oldIndex, newIndex, alias;
      protected string oldTimestamp, newTimestamp;
      protected string oldIndexUrl;
      protected string copyFromUrl;
      protected string faceAdminDir;

      protected ESConnection esConnection;

      protected int sleepAfterExtract;
      protected int maxErrors;
      protected bool mustCopyExisting;
      protected bool fullImport;
      protected bool handleExceptions;

      protected ImportScriptBase () {
         logger = Logs.ErrorLog;
         maxErrors = 0;
      }

      public object OnError (PipelineContext ctx, object value) {
         if (!handleExceptions || --maxErrors < 0) {
            ctx.ActionFlags &= ~_ActionFlags.Handled;
            return null;
         }
         var e = (Exception)value;
         logger.Log (_LogType.ltError, "Error while processing {0}: {1}.", idInfo, e.GetBestMessage ());
         Logs.ErrorLog.Log (e, "Error while processing {0}: {1}.", idInfo, e.GetBestMessage ());

         return null;
      }

      protected void WaitAfterExtract() {
         if (sleepAfterExtract>0) Thread.Sleep (sleepAfterExtract);
      }

      protected void Init(PipelineContext ctx, bool exceptIfNotESEndpoint, int maxErrors=50 ) {
         logger = ctx.ImportLog;
         this.handleExceptions = false;
         this.maxErrors = maxErrors;
         fullImport = (ctx.ImportFlags & _ImportFlags.FullImport) != 0;
         faceAdminDir = XmlUtils.ReadPath (ctx.ImportEngine.Xml.DocumentElement, "faces_admin/@dir", null);

         var dsNode = ctx.DatasourceAdmin.ContextNode;
         sleepAfterExtract = ctx.DatasourceAdmin.ContextNode.ReadInt ("@sleep_after_extract", 0);
         copyFromUrl = dsNode.ReadStr ("copy_from/@url", null);
         mustCopyExisting = false;
         var ep = ctx.Action.Endpoint as ESDataEndpoint;
         if (ep == null) {
            if (exceptIfNotESEndpoint) throw new BMException ("Endpoint is not an ES endpoint but [{0}].", ctx.Action.Endpoint?.GetType ().FullName);
            return;
         }

         esConnection = ep.Connection;
         newIndex = Utils.GetRealIndexName (ep, null, out newTimestamp);
         alias = Utils.GetIndexWithoutTimeStamp (newIndex);
         oldIndex = Utils.GetRealIndexName (ep, alias, out oldTimestamp);
         oldIndexUrl = new Uri (esConnection.BaseUri, alias).ToString ();

         if (copyFromUrl != null) {
            var req = Utils.CreateESRequest (copyFromUrl);
            var fpCopy = Utils.GetIndexFingerPrint (req.Connection, req.IndexName);
            var fpOld = Utils.GetIndexFingerPrint (esConnection, alias);
            mustCopyExisting = fpCopy != fpOld;
            if (!mustCopyExisting) copyFromUrl = null;
         }
      }

      protected string[] ReadFaceNames() {
         string fn = Path.Combine (faceAdminDir, "facenames.txt");
         string[] ret = File.ReadLines (fn).ToArray ();
         logger.Log ("Read {0} face names from {1}", ret.Length, fn);
         return ret;
      }
   }
}
