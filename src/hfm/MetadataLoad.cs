using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALOADACVLib;

using Command;


namespace HFM
{

    public class MetadataLoad
    {

        [Setting("Accounts"),
         Setting("SystemAccounts"),
         Setting("AppSettings"),
         Setting("CellTextLabels", Since = "11.1.2.2"),
         Setting("CheckIntegrity"),
         Setting("ClearAccounts", Deprecated = "11.1.2.2"),
         Setting("ClearAll", Since = "11.1.2.2"),
         Setting("ClearConsolMethods", Deprecated = "11.1.2.2"),
         Setting("ClearCurrencies", Deprecated = "11.1.2.2"),
         Setting("ClearCustom1", Deprecated = "11.1.2.2"),
         Setting("ClearCustom2", Deprecated = "11.1.2.2"),
         Setting("ClearCustom3", Deprecated = "11.1.2.2"),
         Setting("ClearCustom4", Deprecated = "11.1.2.2"),
         Setting("ClearEntities", Deprecated = "11.1.2.2"),
         Setting("ClearScenarios", Deprecated = "11.1.2.2"),
         Setting("Currencies"),
         Setting("Custom1", Deprecated = "11.1.2.2"),
         Setting("Custom2", Deprecated = "11.1.2.2"),
         Setting("Custom3", Deprecated = "11.1.2.2"),
         Setting("Custom4", Deprecated = "11.1.2.2"),
         Setting("CustomX", Since = "11.1.2.2"),
         Setting("Delimiter", ParameterType = typeof(string)), // TODO: Validation list
         Setting("Entities"),
         Setting("FileFormat", ParameterType = typeof(HSV_METALOADEX_FILE_FORMAT), EnumPrefix = "HSV_METALOADEX_"),
         Setting("ICP"),
         Setting("LoadSystemMembers"),
         Setting("Prescan"),
         Setting("Scenarios"),
         Setting("UseReplaceMode"),
         Setting("Values")]
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(MetadataLoad mdl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_METADATALOAD_OPTION), mdl.HsvMetadataLoad.LoadOptions)
            {
            }
        }


        [Setting("Accounts"),
         Setting("SystemAccounts"),
         Setting("AppSettings"),
         Setting("CellTextLabels", Since = "11.1.2.2"),
         Setting("ConsolMethods"),
         Setting("Currencies"),
         Setting("Custom1", Deprecated = "11.1.2.2"),
         Setting("Custom2", Deprecated = "11.1.2.2"),
         Setting("Custom3", Deprecated = "11.1.2.2"),
         Setting("Custom4", Deprecated = "11.1.2.2"),
         Setting("CustomX", Since = "11.1.2.2"),
         Setting("Delimiter", ParameterType = typeof(string)), // TODO: Validation list
         Setting("Entities"),
         Setting("ExtractSystemMembers"),
         Setting("FileFormat", ParameterType = typeof(HSV_METALOADEX_FILE_FORMAT), EnumPrefix = "HSV_METALOADEX_"),
         Setting("ICPs"),
         Setting("Scenarios"),
         Setting("Values")]
        public class ExtractOptions : LoadExtractOptions
        {
            [Factory]
            public ExtractOptions(MetadataLoad mdl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_METADATAEXTRACT_OPTION), mdl.HsvMetadataLoad.ExtractOptions)
            {
            }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvMetadataLoadACV object
        internal readonly HsvMetadataLoadACV HsvMetadataLoad;


        [Factory]
        public MetadataLoad(Session session)
        {
            HsvMetadataLoad = new HsvMetadataLoadACV();
            HsvMetadataLoad.SetSession(session.HsvSession);
        }


        [Command("Loads an HFM application's metadata from a native ASCII or XML file")]
        public void LoadMetadata(
                [Parameter("Path to the source metadata extract file")]
                string extractFile,
                [Parameter("Path to the load log file; if not specified, defaults to same path " +
                             "and name as the source metadata file.",
                 DefaultValue = null)]
                string logFile,
                LoadOptions options)
        {
        }


        [Command("Extracts an HFM application's metadata to a native ASCII or XML file")]
        public void ExtractMetadata(
                [Parameter("Path to the generated metadata extract file")]
                string extractFile,
                [Parameter("Path to the extract log file; if not specified, defaults to same path " +
                           "and name as extract file.", DefaultValue = null)]
                string logFile,
                ExtractOptions options)
        {
            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(extractFile, ".log");
            }
            // TODO: Display options etc
            _log.FineFormat("    Extract file: {0}", extractFile);
            _log.FineFormat("    Log file:     {0}", logFile);
            // TODO: Ensure extractFile and logFile are writeable locations
            HFM.Try("Extracting metadata",
                    () => HsvMetadataLoad.Extract(extractFile, logFile));
        }

    }

}
