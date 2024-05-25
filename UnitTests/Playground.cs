using Microsoft.VisualStudio.TestTools.UnitTesting;
using AlbumImporter;
using Bitmanager.Test;
using Bitmanager.Json;
using Bitmanager.Xml;
using Bitmanager.ImportPipeline;
using Bitmanager.Core;
using Bitmanager.IO;

namespace UnitTests {
   /// <summary>
   /// This is not realy a test case. More a playground to quickly check something
   /// </summary>
   [TestClass]
   public class PlayGroundTests : TestBaseSimple {
      private string root; 
      public PlayGroundTests() {
         root = testAssembly.FindDirectoryToRoot ("data", FindToTootFlags.Except);
      }


      [TestMethod]
      public void TestPipeline () {
         JsonObjectValue result;
         result = execPipeline (@"Some file");
         //Check the result object
      }

      private JsonObjectValue execPipeline (string file) {
         var xml = new XmlHelper(Path.Combine (root, "playground.xml"));
         xml.WriteVal ("datasources/datasource/id/@file", file);
         using (var engine = new ImportEngine ()) {
            engine.Load (xml);
            var report = engine.Import ();
            if (report.ErrorState != _ErrorState.OK)
               throw new BMException (report.ErrorMessage);
            var output = engine.Output;
            if (output == null) throw new BMException ("Pipeline generated no output");
            Assert.AreEqual (1, output.Count);
            logger.Log (output[0].ToJsonString ());
            return output[0];
         }
      }

   }
}
