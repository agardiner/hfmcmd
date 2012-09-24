using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALOADACVLib;

using Command;
using HFMCmd;


namespace HFM
{

    public class MetadataLoad
    {

        /// <summary>
        /// Collection class holding options that can be specified when loading
        /// metadata.
        /// </summary>
        [Setting("Accounts", "Load Accounts dimension"),
         Setting("SystemAccounts", "Load System Accounts (also requires the LoadSystemMembers setting to be set)"),
         Setting("AppSettings", "Load Application Settings"),
         Setting("CellTextLabels", "Load CellText labels", Since = "11.1.2.2"),
         Setting("CheckIntegrity", "Check application integrity against current application before loading"),
         Setting("ClearAccounts", "Clear Accounts dimension before load",
                 Deprecated = "11.1.2.2"),
         Setting("ClearAll", "Clear ALL existing dimension members before load",
                 Since = "11.1.2.2"),
         Setting("ClearConsolMethods", "Clear Consolidation methods",
                 Deprecated = "11.1.2.2"),
         Setting("ClearCurrencies", "Clear Currency dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearCustom1", "Clear Custom1 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearCustom2", "Clear Custom2 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearCustom3", "Clear Custom3 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearCustom4", "Clear Custom4 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearEntities", "Clear Entity dimension",
                 Deprecated = "11.1.2.2"),
         Setting("ClearScenarios", "Clear Scenario dimension",
                 Deprecated = "11.1.2.2"),
         Setting("Currencies", "Clear Currencies dimension"),
         Setting("Custom1", "Load Custom1 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("Custom2", "Load Custom2 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("Custom3", "Load Custom3 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("Custom4", "Load Custom4 dimension",
                 Deprecated = "11.1.2.2"),
         Setting("CustomDims", "Load Custom dimensions (takes an array of booleans, " +
                 "corresponding to each Custom dimension)", ParameterType = typeof(bool[]),
                 Since = "11.1.2.2"),
         Setting("Delimiter", "Metadata file delimiter",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("Entities", "Load Entity dimension"),
         Setting("FileFormat", "Metadata file format",
                 ParameterType = typeof(HSV_METALOADEX_FILE_FORMAT), EnumPrefix = "HSV_METALOADEX_"),
         Setting("ICP", "Load ICP dimension"),
         Setting("LoadSystemMembers", "Load System members"),
         Setting("scanOnly", "Scan metadata file for syntax errors (instead of loading it)"),
         Setting("Scenarios", "Load Scenario dimension"),
         Setting("UseReplaceMode", "Use Replace mode, so that existing metadata is replaced by this load. " +
                 "If this option is set, existing metadata not in the metadata file is removed."),
         Setting("Values", "Load Value dimension members")]
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(MetadataLoad mdl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_METADATALOAD_OPTION), mdl.HsvMetadataLoad.LoadOptions)
            {
            }
        }


        /// <summary>
        /// Collection class holding options that can be specified when extracting
        /// metadata.
        /// </summary>
        [Setting("Accounts", "Extract accounts"),
         Setting("SystemAccounts", "Extract system accounts"),
         Setting("AppSettings", "Extract application settings"),
         Setting("CellTextLabels", "Extract CellText labels", Since = "11.1.2.2"),
         Setting("ConsolMethods", "Extract consolidation methods"),
         Setting("Currencies", "Extract currencies"),
         Setting("Custom1", "Extract Custom1 dimension", Deprecated = "11.1.2.2"),
         Setting("Custom2", "Extract Custom2 dimension", Deprecated = "11.1.2.2"),
         Setting("Custom3", "Extract Custom3 dimension", Deprecated = "11.1.2.2"),
         Setting("Custom4", "Extract Custom4 dimension", Deprecated = "11.1.2.2"),
         Setting("CustomDims", "Extract Custom dimensions", ParameterType = typeof(bool[]),
                 Since = "11.1.2.2"),
         Setting("Delimiter", "File delimiter used in metadata file",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("Entities", "Extract Entity dimension"),
         Setting("ExtractSystemMembers", "Extract system members"),
         Setting("FileFormat", "Metadata file format",
                 ParameterType = typeof(HSV_METALOADEX_FILE_FORMAT), EnumPrefix = "HSV_METALOADEX_"),
         Setting("ICPs", "Extract ICP dimension"),
         Setting("Scenarios", "Extract Scenario dimension"),
         Setting("Values", "Extract Value dimension")]
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
                string metadataFile,
                [Parameter("Path to the load log file; if not specified, defaults to same path " +
                             "and name as the source metadata file.",
                 DefaultValue = null)]
                string logFile,
                LoadOptions options)
        {
            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(metadataFile, ".log");
            }

            // Ensure metadata file exists and logFile is writeable
            Utilities.EnsureFileExists(metadataFile);
            Utilities.EnsureFileWriteable(logFile);

            HFM.Try("Loading metadata",
                    () => HsvMetadataLoad.Load(metadataFile, logFile));
        }


        [Command("Extracts an HFM application's metadata to a native ASCII or XML file")]
        public void ExtractMetadata(
                [Parameter("Path to the generated metadata extract file")]
                string metadataFile,
                [Parameter("Path to the extract log file; if not specified, defaults to same path " +
                           "and name as extract file.", DefaultValue = null)]
                string logFile,
                ExtractOptions options)
        {
            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(metadataFile, ".log");
            }
            // TODO: Display options etc
            _log.FineFormat("    Metadata file: {0}", metadataFile);
            _log.FineFormat("    Log file:     {0}", logFile);

            // Ensure extractFile and logFile are writeable locations
            Utilities.EnsureFileWriteable(metadataFile);
            Utilities.EnsureFileWriteable(logFile);

            HFM.Try("Extracting metadata",
                    () => HsvMetadataLoad.Extract(metadataFile, logFile));
        }

    }

}
