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
                 "accumulate, rather than overwriting one another"),
         Setting("AppendToLog", "If true, any existing log file is appended to, instead of overwritten"),
         Setting("ContainsShares", "Indicates whether the data file contains shares data, " +
                 "such as Shares Outstanding, Voting Outstanding, or Owned"),
         Setting("ContainsSubmissionPhase", "Indicates whether the data file contains data for " +
                 "phased submissions"),
         Setting("Delimiter", "Data file delimiter",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("DecimalChar", "The decimal character used within the data file",
                 ParameterType = typeof(string)),
         Setting("ThousandsChar", "The thousands separator character used within the data file",
                 ParameterType = typeof(string)),
         Setting("UpdateMode", "Specifies how data loads affect existing data values",
                 ParameterType = typeof(EDataLoadUpdateMode),
                 DefaultValue = EDataLoadUpdateMode.Merge),
         Setting("ScanOnly", "Scan data file for syntax errors (instead of loading it)")]
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
        [Setting("AppendToLog", "If true, any existing log file is appended to, instead of overwritten"),
         Setting("Delimiter", "File delimiter used in data extract file",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("IncludeCalculatedData", "If true, the data extract includes calculated data"),
         Setting("View", "The view of data (i.e. periodic, YTD, etc) to extract",
                 ParameterType = typeof(EDataView))]
         // TODO: Add support for member subsets
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
                [Parameter("Path to the source data file")]
                string dataFile,
                [Parameter("Path to the load log file; if not specified, defaults to same path " +
                             "and name as the source data file.",
                 DefaultValue = null)]
                string logFile,
                LoadOptions options)
        {
            object oErrors = null;

            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(dataFile, ".log");
            }

            // Ensure data file exists and logFile is writeable
            Utilities.FileExists(dataFile);
            Utilities.FileWriteable(logFile);

            HFM.Try("Loading data",
                    () => oErrors = HsvcDataLoad.Load2(dataFile, logFile));
            if((bool)oErrors) {
                _log.Warn("Data load resulted in errors; check log file for details");
                // TODO:  Should we show the warnings here?
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
                ExtractOptions options)
        {
            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(dataFile, ".log");
            }
            // TODO: Display options etc
            _log.FineFormat("    Data file: {0}", dataFile);
            _log.FineFormat("    Log file:     {0}", logFile);

            // Ensure dataFile and logFile are writeable locations
            Utilities.FileWriteable(dataFile);
            Utilities.FileWriteable(logFile);

            HFM.Try("Extracting data",
                    () => HsvcDataLoad.Extract(dataFile, logFile));
        }

    }

}
