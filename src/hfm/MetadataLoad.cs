using System;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALOADACVLib;

using Command;


namespace HFM
{

    public class MetadataLoad
    {
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(MetadataLoad mdl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_METADATALOAD_OPTION), mdl.HsvMetadataLoad.LoadOptions) { }
        }


        public class ExtractOptions : LoadExtractOptions
        {
            [Factory]
            public ExtractOptions(MetadataLoad mdl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_METADATAEXTRACT_OPTION), mdl.HsvMetadataLoad.ExtractOptions) { }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvMetadataLoadACV object
        internal readonly HsvMetadataLoadACV HsvMetadataLoad;


        [Factory]
        public MetadataLoad(Session session)
        {
            HsvMetadataLoad = new HsvMetadataLoadACVClass();
            HsvMetadataLoad.SetSession(session.HsvSession);
        }

        [Command]
        public void Test() { new LoadOptions(this); new ExtractOptions(this); }


        [Command,
         Description("Loads an HFM application's metadata from a native ASCII or XML file")]
        public void LoadMetadata(
                [Description("Path to the source metadata extract file")] string extractFile,
                [Description("Path to the log file containing details of the load process")] string logFile,
                LoadOptions options)
        {
        }


        [Command,
         Description("Extracts an HFM application's metadata to a native ASCII or XML file")]
        public void ExtractMetadata(
                [Description("Path to the generated metadata extract file")] string extractFile,
                [Description("Path to the extract log file")] string logFile,
                ExtractOptions options)
        {
        }

    }

}
