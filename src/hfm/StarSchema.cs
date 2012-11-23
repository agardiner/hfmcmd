using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVSTARSCHEMAACMLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Defines the star schema push options
    /// </summary>
    public enum EPushType
    {
        Create = SS_PUSH_OPTIONS.ssCREATE,
        Update = SS_PUSH_OPTIONS.ssUPDATE
    }


    /// <summary>
    /// Defines the star schema extract type (i.e. layout) options
    /// </summary>
    public enum EStarSchemaExtractType
    {
        Standard = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_STANDARD,
        MetadataAll = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_METADATA_ALL,
        MetadataSelected = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_METADATA_SELECTED,
        SQLAggregation = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_SQL_AGG,
        Essbase = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_ESSBASE,
        Warehouse = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_WAREHOUSE
    }

    /// <summary>
    /// Defines the flat file extract types
    /// </summary>
    public enum EFileExtractType
    {
        FlatFile = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE,
        FlatFileNoHeader = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE_NOHEADER
    }

    /// <summary>
    /// Enumeration defininig the extract options for line-item detail cells.
    /// </summary>
    public enum ELineItems
    {
        Exclude = EA_LINEITEM_OPTIONS.EA_LINEITEM_EXCLUDE,
        Summary = EA_LINEITEM_OPTIONS.EA_LINEITEM_SUMMARY,
        Detail = EA_LINEITEM_OPTIONS.EA_LINEITEM_DETAIL
    }

    /// <summary>
    /// Enumeration defininig the EA extract process statuses
    /// </summary>
    public enum EEATaskStatus
    {
        Blocked = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_BLOCKED,
        Cancelled = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_CANCELLED,
        Complete = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_COMPLETE,
        CompleteWithErrors = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_COMPLETE_W_ERRORS,
        ExtractingData = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_DATA,
        EssbaseAggregation = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_ESSBASE_AGG,
        Initializing = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_INITIALIZING,
        Metadata = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_METADATA,
        Queued = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_QUEUED,
        SQLAggregation = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_SQL_AGG
    }


    /// <summary>
    /// Class for working with HFM Extended Analytics extracts.
    /// </summary>
    public class StarSchema
    {

        [Setting("POV", "A Point-of-View expression, such as 'S#Actual.Y#2010.P#May." +
                 "W#YTD.E#E1.V#<Entity Currency>'. Use a POV expression to select members " +
                 "from multiple dimensions in one go. Note that if a dimension member is " +
                 "specified in the POV expression and via a setting for the dimension, the " +
                 "dimension setting takes precedence.",
                 ParameterType = typeof(string), Order = 0),
         Setting("Scenario", "Scenario member(s) to include in the slice definition",
                 Alias = "Scenarios", ParameterType = typeof(string), Order = 2),
         Setting("Year", "Year member(s) to include in the slice definition",
                 Alias = "Years", ParameterType = typeof(string), Order = 3),
         Setting("Period", "Period member(s) to include in the slice definition",
                 Alias = "Periods", ParameterType = typeof(string), Order = 4),
         Setting("View", "View member(s) to include in the slice definition",
                 Alias = "Views", ParameterType = typeof(string), Order = 5,
                 DefaultValue = "<Scenario View>"),
         Setting("Entity", "Entity member(s) to include in the slice definition",
                 Alias = "Entities", ParameterType = typeof(string), Order = 6),
         Setting("Value", "Value member(s) to include in the slice definition",
                 Alias = "Values", ParameterType = typeof(string), Order = 7),
         Setting("Account", "Account member(s) to include in the slice definition",
                 Alias = "Accounts", ParameterType = typeof(string), Order = 8),
         Setting("ICP", "ICP member(s) to include in the slice definition",
                 Alias = "ICPs", ParameterType = typeof(string), Order = 9),
         DynamicSetting("CustomDimName", "<CustomDimName> member(s) to include in the slice definition",
                 ParameterType = typeof(string), Order = 10)]
        public class ExtractSpecification : Slice, IDynamicSettingsCollection
        {
            [Factory(SingleUse = true)]
            public ExtractSpecification(Metadata metadata) : base(metadata) {}
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to the current session
        protected readonly Session Session;
        // Reference to HFM HsvStarSchemaACM object
        protected readonly HsvStarSchemaACM HsvStarSchemaACM;


        [Factory]
        public StarSchema(Session session)
        {
            Session = session;
            HsvStarSchemaACM = new HsvStarSchemaACM();
            HsvStarSchemaACM.SetSession(session.HsvSession);
        }


        [Command("Lists the names of registered DSNs containing database connection details to " +
                 "use for Extended Analytics star schema extracts")]
        public void EnumRegisteredDSNs(IOutput output)
        {
            object oDSNs = null;
            HFM.Try("Retrieving DSN names",
                    () => oDSNs = HsvStarSchemaACM.EnumRegisteredDSNs());

            if(oDSNs != null) {
                var dsns = oDSNs as Array;
                output.WriteEnumerable(dsns, "DSN");
            }
        }


        [Command("Deletes the tables associated with a star schema extract")]
        public void DeleteStarSchema(
                [Parameter("The name of the DSN that contains the connection details for the " +
                           "database where the star schema extract is to be created. This DSN " +
                           "must exist on the HFM server, and have been registered via the " +
                           "HFM Configuration utility.")]
                string DSN,
                [Parameter("The prefix that should appear at the start of each table name created " +
                           "by the extract process.")]
                string tablePrefix)
        {
            HFM.Try("Deleting star schema",
                    () => HsvStarSchemaACM.DeleteStarSchema(DSN, tablePrefix));
        }


        [Command("Extracts HFM data to a set of tables in a relational database")]
        public void ExtractDataToStarSchema(
                [Parameter("The name of the DSN that contains the connection details for the " +
                           "database where the star schema extract is to be created. This DSN " +
                           "must exist on the HFM server, and have been registered via the " +
                           "HFM Configuration utility.")]
                string DSN,
                [Parameter("The prefix that should appear at the start of each table name created " +
                           "by the extract process.")]
                string tablePrefix,
                [Parameter("Whether to delete any existing data before performing the extract",
                           DefaultValue = true)]
                bool deleteExisting,
                [Parameter("The type of star schema to produce.")]
                EStarSchemaExtractType extractType,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccts,
                [Parameter("Whether to include calculated data",
                           DefaultValue = true, Since = "11.1.1")]
                bool includeCalculatedData,
                [Parameter("Whether to include derived data",
                           DefaultValue = true, Since = "11.1.1")]
                bool includeDerivedData,
                [Parameter("The path to where the EA extract log file should be generated; " +
                           "if omitted, no log file is created.", DefaultValue = null)]
                string logFile,
                ExtractSpecification slice,
                IOutput output)
        {
            DoEAExtract(DSN, tablePrefix, (SS_PUSH_OPTIONS)(deleteExisting ? EPushType.Create : EPushType.Update),
                        (EA_EXTRACT_TYPE_FLAGS)extractType, includeDynamicAccts, includeCalculatedData,
                        includeDerivedData, false, false, (EA_LINEITEM_OPTIONS)ELineItems.Summary, "",
                        logFile, slice, output);
        }


        [Command("Extracts HFM data to a flat file",
                 Since = "11.1.1")]
        public void ExtractDataToFlatFile(
                [Parameter("The file name prefix")]
                string filePrefix,
                [Parameter("Include file header containing extract details",
                           DefaultValue = false)]
                bool includeHeader,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccts,
                [Parameter("Whether to include calculated data",
                           DefaultValue = true)]
                bool includeCalculatedData,
                [Parameter("Whether to include derived data",
                           DefaultValue = true)]
                bool includeDerivedData,
                [Parameter("Level of detail to be extracted for line item detail accounts",
                           DefaultValue = ELineItems.Summary)]
                ELineItems lineItems,
                [Parameter("The field delimiter to use",
                           DefaultValue = ";")]
                string delimiter,
                [Parameter("The path to where the EA extract log file should be generated; " +
                           "if omitted, no log file is created.", DefaultValue = null)]
                string logFile,
                ExtractSpecification slice,
                IOutput output)
        {
            DoEAExtract("", filePrefix, (SS_PUSH_OPTIONS)EPushType.Create,
                        (EA_EXTRACT_TYPE_FLAGS)(includeHeader ? EFileExtractType.FlatFile :
                                                                EFileExtractType.FlatFileNoHeader),
                        includeDynamicAccts, includeCalculatedData, includeDerivedData,
                        false, false, (EA_LINEITEM_OPTIONS)lineItems, delimiter, logFile, slice,
                        output);

            // TODO: Download the extract file
        }


        private void DoEAExtract(string dsn, string prefix, SS_PUSH_OPTIONS pushType,
                EA_EXTRACT_TYPE_FLAGS extractType, bool includeDynamicAccts, bool includeCalculatedData,
                bool includeDerivedData, bool includeCellText, bool includePhasedSubmissionGroupData,
                EA_LINEITEM_OPTIONS lineItems, string delimiter, string logFile,
                ExtractSpecification slice, IOutput output)
        {
            int taskId = 0;

            // Check user is permitted to run EA extracts
            Session.Security.CheckPermissionFor(ETask.ExtendedAnalytics);

            // Perform the EA extract
            _log.InfoFormat("Extracting data to {0}", slice);
            if(HFM.HasVariableCustoms) {
                HFM.Try(() => HsvStarSchemaACM.CreateStarSchemaExtDim(dsn, prefix, pushType,
                                extractType, includeDynamicAccts, includeCalculatedData, includeDerivedData,
                                lineItems, includeCellText, includePhasedSubmissionGroupData, delimiter,
                                slice.HfmSliceCOM, out taskId));
                _log.DebugFormat("Task id: {0}", taskId);
            }
            else {
                HFM.Try(() => HsvStarSchemaACM.CreateStarSchema(dsn, prefix, pushType,
                                extractType, !includeDynamicAccts, slice.Scenarios.MemberIds,
                                slice.Years.MemberIds, slice.Periods.MemberIds, slice.Views.MemberIds,
                                slice.Entities.MemberIds, slice.Entities.ParentIds, slice.Values.MemberIds,
                                slice.Accounts.MemberIds, slice.ICPs.MemberIds, slice.Custom1.MemberIds,
                                slice.Custom2.MemberIds, slice.Custom3.MemberIds, slice.Custom4.MemberIds));
            }

            // Monitor progress
            MonitorEAExtract(output);

            // Retrieve log file
            if(logFile != null) {
                RetrieveLog(logFile);
            }
        }


        /// Monitors the progress of an EA extract
        private void MonitorEAExtract(IOutput output)
        {
            int errorCode = 0;
            bool isRunning = false;
            double numComplete = 0;
            double numRecords = 0;
            var status = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_INITIALIZING;
            var taskStatus = EEATaskStatus.Initializing;
            var lastTaskStatus = EEATaskStatus.Initializing;

            var pm = new ProgressMonitor(output);
            pm.MonitorProgress((bool cancel, out bool running) => {
                HFM.Try("Retrieving task status",
                        () => HsvStarSchemaACM.GetAsynchronousTaskStatus(out status, out numRecords,
                                    out numComplete, out isRunning, out errorCode));
                taskStatus = (EEATaskStatus)status;
                running = isRunning;

                if(cancel && running) {
                    HFM.Try("Cancelling task", () => HsvStarSchemaACM.QuitAsynchronousTask());
                }
                else if(taskStatus != lastTaskStatus) {
                    switch(taskStatus) {
                        case EEATaskStatus.Complete:
                        case EEATaskStatus.CompleteWithErrors:
                        case EEATaskStatus.Cancelled:
                            break;
                        default:
                            _log.InfoFormat("Extract Status: {0}", taskStatus);
                            output.Operation = taskStatus.ToString();
                            break;
                    }
                    lastTaskStatus = taskStatus;
                }
                return (int)(numComplete / numRecords * 100);
            });

            switch(taskStatus) {
                case EEATaskStatus.Complete:
                    _log.Info("Star schema extract completed successfully");
                    break;
                case EEATaskStatus.CompleteWithErrors:
                    _log.Error("Star schema extract completed with errors");
                    throw new HFMException(errorCode);
                case EEATaskStatus.Cancelled:
                    _log.Warn("Star schema extract was cancelled");
                    break;
            }
        }


        /// Retrieves the log file for the last extended analytics extract.
        private void RetrieveLog(string logFile)
        {
            string log = null;
            bool hasLog = false;

            Utilities.EnsureFileWriteable(logFile);
            HFM.Try("Retrieving EA extract log file",
                    () => HsvStarSchemaACM.GetExtractLogData(out log, out hasLog));
            if(hasLog) {
                using(var eaLog = new StreamWriter(logFile)) {
                    eaLog.Write(log);
                }
            }
        }

    }

}
