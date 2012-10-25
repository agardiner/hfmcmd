using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVCDATALOADLib;

using Command;
using HFMCmd;


namespace HFM
{

    public enum EDataLoadUpdateMode
    {
        Merge = HSV_DATALOAD_DUPLICATE_OPTIONS.HSV_DATALOAD_MERGE,
        Replace = HSV_DATALOAD_DUPLICATE_OPTIONS.HSV_DATALOAD_REPLACE,
        Accumulate = HSV_DATALOAD_DUPLICATE_OPTIONS.HSV_DATALOAD_ACCUMULATE,
        ReplaceWithSecurity = HSV_DATALOAD_DUPLICATE_OPTIONS.HSV_DATALOAD_REPLACEWITHSECURITY
    }


    public enum EDataView
    {
        Periodic = HSV_DATA_VIEW.HSV_DATA_VIEW_PERIODIC,
        YTD = HSV_DATA_VIEW.HSV_DATA_VIEW_YTD,
        ScenarioDefault = HSV_DATA_VIEW.HSV_DATA_VIEW_SCENARIO
    }



    public class DataLoad
    {

        /// <summary>
        /// Collection class holding options that can be specified when loading
        /// data.
        /// </summary>
        [Setting("AccumulateWithinFile", "If set, then multiple data values for the same cell " +
                 "accumulate, rather than overwriting one another",
                 InternalName = "Accumulate within file"),
         Setting("AppendToLog", "If true, any existing log file is appended to, instead of overwritten",
                 InternalName = "Append to Log File"),
         Setting("ContainsShares", "Indicates whether the data file contains shares data, " +
                 "such as Shares Outstanding, Voting Outstanding, or Owned",
                 InternalName = "Does the file contain shares data"),
         Setting("ContainsSubmissionPhase", "Indicates whether the data file contains data for " +
                 "phased submissions",
                 InternalName = "Does the file contain submission phase data"),
         Setting("Delimiter", "Data file delimiter",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("DecimalChar", "The decimal character used within the data file",
                 ParameterType = typeof(string)),
         Setting("ThousandsChar", "The thousands separator character used within the data file",
                 ParameterType = typeof(string)),
         Setting("UpdateMode", "Specifies how data loads affect existing data values",
                 InternalName = "Duplicates",
                 ParameterType = typeof(EDataLoadUpdateMode),
                 DefaultValue = EDataLoadUpdateMode.Merge),
         // TODO: Work out how to map a bool to an enum value
         Setting("ScanOnly", "Scan data file for syntax errors (instead of loading it)",
                 InternalName = "Mode")]
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(DataLoad dl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_DATALOAD_OPTION), dl.HsvcDataLoad.LoadOptions)
            {
            }
        }


        /// <summary>
        /// Collection class holding options that can be specified when extracting
        /// data.
        /// </summary>
        [Setting("AppendToLog", "If true, any existing log file is appended to, instead of overwritten",
                 InternalName = "Append to Log File"),
         Setting("Delimiter", "File delimiter used in data extract file",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("IncludeCalculatedData", "If true, the data extract includes calculated data",
                 InternalName = "Extract Calculated"),
         Setting("IncludePhasedGroups", "If true, includes phased groups in data extract",
                 InternalName = "Extract Phased Groups"),
         Setting("View", "The view of data (i.e. Periodic, YTD, or ScenarioDefault) to extract",
                 ParameterType = typeof(EDataView))]
        public class ExtractOptions : LoadExtractOptions
        {
            [Factory]
            public ExtractOptions(DataLoad dl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_DATAEXTRACT_OPTION), dl.HsvcDataLoad.ExtractOptions)
            {
            }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvcDataLoad object
        internal readonly HsvcDataLoad HsvcDataLoad;


        [Factory]
        public DataLoad(Session session)
        {
            HsvcDataLoad = new HsvcDataLoad();
            HsvcDataLoad.SetSession(session.HsvSession);
        }


        [Command("Loads data to an HFM application from a text file")]
        public void LoadData(
                [Parameter("Path to the source data file(s). To load multiple files from the same " +
                           "source directory, use wildcards in the file name")]
                string dataFiles,
                [Parameter("Path to the folder in which to create log files; if not specified, defaults to same folder " +
                           "as the source data file. Log files have the same file name (but with a .log extension) as " +
                           "the file from which the data is loaded",
                 DefaultValue = null)]
                string logDir,
                LoadOptions options,
                SystemInfo si,
                IOutput output)
        {
            object oErrors = null;
            bool didError = false;
            string logFile;

            var paths = Utilities.GetMatchingFiles(dataFiles);
            if(paths.Length > 1) {
                _log.InfoFormat("Found {0} data files to process", paths.Length);
            }
            foreach(var dataFile in paths) {
                if(logDir == null || logDir == "") {
                    logFile = Path.ChangeExtension(dataFile, ".log");
                }
                else {
                    logFile = Path.Combine(logDir, Path.ChangeExtension(
                                    Path.GetFileName(dataFile), ".log"));
                }

                // Ensure data file exists and logFile is writeable
                Utilities.EnsureFileExists(dataFile);
                Utilities.EnsureFileWriteable(logFile);

                _log.InfoFormat("Loading data from {0}", dataFile);
                HFM.Try("Loading data", () => {
                    si.MonitorBlockingTask(output);
                    oErrors = HsvcDataLoad.Load2(dataFile, logFile);
                    si.BlockingTaskComplete();
                });

                if((bool)oErrors) {
                    _log.WarnFormat("Data load resulted in errors; check log file {0} for details",
                            Path.GetFileName(logFile));
                    // TODO:  Should we show the warnings here?
                    didError = true;
                }
            }

            if(didError) {
                throw new HFMException("One or more errors occurred while loading data");
            }
        }


        [Command("Extracts data from an HFM application to a text file")]
        public void ExtractData(
                [Parameter("Path to the generated data extract file")]
                string dataFile,
                [Parameter("Path to the extract log file; if not specified, defaults to same path " +
                           "and name as extract file.", DefaultValue = null)]
                string logFile,
                [Parameter("The scenario to include in the extract")]
                string scenario,
                [Parameter("The year to include in the extract")]
                string year,
                [Parameter("The period(s) to include in the extract",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entities to include in the extract",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The accounts to include in the extract",
                           Alias = "Account")]
                string[] accounts,
                ExtractOptions options,
                Metadata metadata)
        {
            options["Scenario"] = metadata["Scenario"].GetId(scenario);
            options["Year"] = metadata["Year"].GetId(year);
            var entityList = metadata["Entity"].GetMembers(entities);
            options["Entity Subset"] = entityList.MemberIds;
            options["Parent Subset"] = entityList.ParentIds;
            options["Period Subset"] = metadata["Period"].GetMembers(periods).MemberIds;
            options["Account Subset"] = metadata["Account"].GetMembers(accounts).MemberIds;

            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(dataFile, ".log");
            }

            // Ensure dataFile and logFile are writeable locations
            Utilities.EnsureFileWriteable(dataFile);
            Utilities.EnsureFileWriteable(logFile);

            HFM.Try("Extracting data",
                    () => HsvcDataLoad.Extract(dataFile, logFile));
        }

    }

}
