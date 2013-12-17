using System;
using System.IO;
using System.Text;

using log4net;
// We have to include the following lib even when using dynamic, since it contains
// the definition of the enums
using HSVSTARSCHEMAACMLib;

using Command;
using HFMCmd;
using Utilities;


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

#if !HFM_9_3_1
    /// <summary>
    /// Defines the flat file extract types
    /// </summary>
    public enum EFileExtractType
    {
        FlatFile = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE,
        FlatFileNoHeader = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE_NOHEADER
    }
#endif

#if HFM_11_1_2_2
    /// <summary>
    /// Enumeration defininig the extract options for line-item detail cells.
    /// </summary>
    public enum ELineItems
    {
        Exclude = EA_LINEITEM_OPTIONS.EA_LINEITEM_EXCLUDE,
        Summary = EA_LINEITEM_OPTIONS.EA_LINEITEM_SUMMARY,
        Detail = EA_LINEITEM_OPTIONS.EA_LINEITEM_DETAIL
    }
#endif

    /// <summary>
    /// Enumeration defininig the EA extract process statuses
    /// </summary>
    public enum EEATaskStatus
    {
        Blocked = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_BLOCKED,
        Cancelled = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_CANCELLED,
        Complete = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_COMPLETE,
        CompleteWithErrors = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_COMPLETE_W_ERRORS,
        Data = EA_TASK_STATUS_FLAGS.EA_TASK_STATUS_DATA,
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
#if LATE_BIND
        protected readonly dynamic HsvStarSchemaACM;
        protected dynamic HsvStarSchemaTemplates
#else
        protected readonly HsvStarSchemaACM HsvStarSchemaACM;
        protected IHsvStarSchemaTemplates HsvStarSchemaTemplates
#endif
        {
            get {
                return (IHsvStarSchemaTemplates)HsvStarSchemaACM;
            }
        }


        [Factory]
        public StarSchema(Session session)
        {
            _log.Trace("Constructing StarSchema object");
            Session = session;
#if LATE_BIND
            HsvStarSchemaACM = session.HsvSession.CreateObject("Hyperion.HsvStarSchemaACM");
#else
            HsvStarSchemaACM = (HsvStarSchemaACM)session.HsvSession.CreateObject("Hyperion.HsvStarSchemaACM");
#endif
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
                bool includeDynamicAccounts,
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
                        (EA_EXTRACT_TYPE_FLAGS)extractType, includeDynamicAccounts, includeCalculatedData,
                        includeDerivedData, false, false,
#if HFM_11_1_2_2
                        (EA_LINEITEM_OPTIONS)ELineItems.Summary,
#endif
                        "", logFile, slice, output);
        }


#if !HFM_9_3_1
        [Command("Extracts HFM data to a flat file",
                 Since = "11.1.1")]
        public void ExtractDataToFlatFile(
                [Parameter("The path to the extract file to be created")]
                string extractFile,
                [Parameter("Include file header containing extract details",
                           DefaultValue = false)]
                bool includeHeader,
                [Parameter("Flag specifying whether extract file should be decompressed; " +
                           "extracts are generated in GZip (.gz) format, so compressed files " +
                           "are faster to create and smaller in size",
                           DefaultValue = true)]
                bool decompress,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccounts,
                [Parameter("Whether to include calculated data",
                           DefaultValue = true)]
                bool includeCalculatedData,
                [Parameter("Whether to include derived data",
                           DefaultValue = true)]
                bool includeDerivedData,
#if HFM_11_1_2_2
                [Parameter("Level of detail to be extracted for line item detail accounts",
                           DefaultValue = ELineItems.Summary)]
                ELineItems lineItems,
#endif
                [Parameter("The field delimiter to use",
                           DefaultValue = ";")]
                string delimiter,
                [Parameter("The path to where the EA extract log file should be generated; " +
                           "if omitted, no log file is created.", DefaultValue = null)]
                string logFile,
                ExtractSpecification slice,
                IOutput output,
                Client client)
        {
            int taskId = 0;
            try
            {
                taskId = DoEAExtract("", "EA_FILE", (SS_PUSH_OPTIONS)EPushType.Create,
                                (EA_EXTRACT_TYPE_FLAGS)(includeHeader ? EFileExtractType.FlatFile :
                                                                        EFileExtractType.FlatFileNoHeader),
                                includeDynamicAccounts, includeCalculatedData, includeDerivedData,
                                false, false,
#if HFM_11_1_2_2
 (EA_LINEITEM_OPTIONS)lineItems,
#endif
 delimiter, logFile, slice,
                                output);
            }
            catch (Exception ex)
            {
                _log.Warn(string.Format("Unknown Error: {0}", ex.Message), ex);
                throw ex;
            }
            // Get the path to the extract file
            string path = null;
            HFM.Try("Retrieving extract file path",
                    () => Session.SystemInfo.HsvSystemInfo.GetRunningTaskLogFilePathName(taskId, out path));
            _log.DebugFormat("Path: {0}", path);

            // Returns a string containing 3 parts, separated by semi-colons:
            // - the root directory where Oracle middleware is installed
            // - the path relative to that root where the log file is located
            // - the path relative to that root where the data file is located
            var parts = path.Split(';');
            var serverFile = Path.Combine(parts[0], parts[2]);

            // Download the extract file
            var ft = Session.Server.FileTransfer;
            ft.RetrieveFile(Session, serverFile, extractFile, decompress, output);
        }
#endif


        [Command("Returns a list of Extended Analytics extract template names for the current user")]
        public void EnumDataExtractTemplates(IOutput output)
        {
            object oNames = null;
            HFM.Try("Retrieving EA templates",
                    () => oNames = HsvStarSchemaTemplates.EnumTemplates());
            var names = HFM.Object2Array<string>(oNames);
            output.WriteEnumerable(names, "Template Name");
        }


        [Command("Creates an Extended Analytics extract template for a relational database target" +
                 "on the HFM server for the logged in user")]
        public void CreateStarSchemaExtractTemplate(
                [Parameter("The name to give to the template")]
                string templateName,
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
                bool includeDynamicAccounts,
                [Parameter("Whether to include calculated data",
                           DefaultValue = true)]
                bool includeCalculatedData,
                [Parameter("Whether to include derived data",
                           DefaultValue = true)]
                bool includeDerivedData,
                ExtractSpecification slice)
        {
            // TODO: Complete this
            StringBuilder sb = new StringBuilder();
            sb.Append("<povTemplate><povEA>");
            sb.Append(slice);
            sb.Append("</povEA><options><tablePrefix>");
            //sb.Append(spec.TablePrefix);
            sb.Append("</tablePrefix><exportOption>");
            //sb.Append((int)spec.ExtractType);
            sb.Append("</exportOption><selectedDSN>");
            //sb.Append(spec.DataSource);
            sb.Append("</selectedDSN><excludeDynAccts>");
            sb.Append(includeDynamicAccounts ? 0 : -1);
            sb.Append("</excludeDynAccts></options></povTemplate>");

            HFM.Try("Creating star schema template {0}", templateName,
                    () => HsvStarSchemaTemplates.SetTemplate(templateName, sb.ToString(), true));
        }


        [Command("Creates an Extended Analytics extract template on the HFM server for the logged in user")]
        public void CreateFlatFileExtractTemplate(
                [Parameter("The name to give to the template")]
                string templateName,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccounts,
                [Parameter("Whether to include calculated data",
                           DefaultValue = true)]
                bool includeCalculatedData,
                [Parameter("Whether to include derived data",
                           DefaultValue = true)]
                bool includeDerivedData,
#if HFM_11_1_2_2
                [Parameter("Level of detail to be extracted for line item detail accounts",
                           DefaultValue = ELineItems.Summary)]
                ELineItems lineItems,
#endif
                [Parameter("The field delimiter to use",
                           DefaultValue = ";")]
                string delimiter,
                ExtractSpecification slice)
        {
            // TODO: Complete this
            StringBuilder sb = new StringBuilder();
            sb.Append("<povTemplate><povEA>");
            sb.Append(slice);
            sb.Append("</povEA><options><tablePrefix>");
            //sb.Append(spec.TablePrefix);
            sb.Append("</tablePrefix><exportOption>");
            //sb.Append((int)spec.ExtractType);
            sb.Append("</exportOption><selectedDSN>");
            //sb.Append(spec.DataSource);
            sb.Append("</selectedDSN><excludeDynAccts>");
            sb.Append(includeDynamicAccounts ? 0 : -1);
            sb.Append("</excludeDynAccts></options></povTemplate>");

            HFM.Try("Creating star schema template {0}", templateName,
                    () => HsvStarSchemaTemplates.SetTemplate(templateName, sb.ToString(), true));
        }


        [Command("Deletes a data extract template")]
        public void DeleteDataExtractTemplate(
                [Parameter("The name of the template to delete")]
                string templateName)
        {
            HFM.Try("Deleting data extract template {0}", templateName,
                    () => HsvStarSchemaTemplates.DeleteTemplate(templateName));
            _log.InfoFormat("Data extract template {0} deleted", templateName);
        }


        [Command("Downloads a data extract template file")]
        public string SaveDataExtractTemplate(
                [Parameter("The name of the template to download")]
                string templateName,
                [Parameter("The path to the file to create for the data extract template",
                           DefaultValue = null)]
                string templateFile)
        {
            string template = null;

            HFM.Try("Retrieving star schema template {0}", templateName,
                    () => template = HsvStarSchemaTemplates.GetTemplate(templateName));
            _log.InfoFormat("Retrieved template {0}", templateName);

            if(templateFile != null) {
                using(StreamWriter sw = new StreamWriter(templateFile)) {
                    sw.WriteLine(template);
                    sw.Close();
                }
                _log.InfoFormat("Saved template to {0}", templateFile);
            }
            return template;
        }


        [Command("Uploads a data extract template file")]
        public void LoadDataExtractTemplate(
                [Parameter("The path to the data extract template")]
                string templateFile,
                [Parameter("The name to give the template; if omitted, defaults to the name of the file",
                           DefaultValue = null)]
                string templateName,
                [Parameter("Flag indicating whether any existing template should be overwritten",
                           DefaultValue = true)]
                bool overwrite)
        {
            string template = null;

            FileUtilities.EnsureFileExists(templateFile);
            using(var sr = new StreamReader(templateFile)) {
                template = sr.ReadToEnd();
            }
            if(templateName == null) {
                templateName = Path.GetFileNameWithoutExtension(templateFile);
            }

            HFM.Try("Loading extended analytics template from {0}", templateFile,
                    () => HsvStarSchemaTemplates.SetTemplate(templateName, template, overwrite));

            _log.InfoFormat("Uploaded data extract template {0}", templateName);
        }


        // Performs an EA extract to a relational or flat file target
        private int DoEAExtract(string dsn, string prefix, SS_PUSH_OPTIONS pushType,
                EA_EXTRACT_TYPE_FLAGS extractType, bool includeDynamicAccts, bool includeCalculatedData,
                bool includeDerivedData, bool includeCellText, bool includePhasedSubmissionGroupData,
#if HFM_11_1_2_2
                EA_LINEITEM_OPTIONS lineItems,
#endif
                string delimiter, string logFile,
                ExtractSpecification slice, IOutput output)
        {
            int taskId = 0;

            // Check user is permitted to run EA extracts
            Session.Security.CheckPermissionFor(ETask.ExtendedAnalytics);

            // Perform the EA extract
            _log.InfoFormat("Extracting data for {0}", slice);
            try
            {
                if (HFM.HasVariableCustoms)
                {
                    _log.Debug("Variable Customs Enabled");
                    bool includeData = true; // new parameter introduced between 11.1.2.2 and 11.1.2.2.305
#if HFM_11_1_2_2
                    HFM.Try(() => HsvStarSchemaACM.CreateStarSchemaExtDim(dsn, prefix, pushType,
                                    extractType,
#if Patch300
 includeData,
#endif
 includeDynamicAccts, includeCalculatedData, includeDerivedData,
                                    lineItems, includeCellText, includePhasedSubmissionGroupData, delimiter,
                                    slice.HfmSliceCOM, out taskId));
                    _log.DebugFormat("Task id: {0}", taskId);
#else
                    HFM.ThrowIncompatibleLibraryEx();
#endif
                }
                else
                {
                    HFM.Try(() => HsvStarSchemaACM.CreateStarSchema(dsn, prefix, pushType,
                                    extractType, !includeDynamicAccts, slice.Scenario.MemberIds,
                                    slice.Year.MemberIds, slice.Period.MemberIds, slice.View.MemberIds,
                                    slice.Entity.MemberIds, slice.Entity.ParentIds, slice.Value.MemberIds,
                                    slice.Account.MemberIds, slice.ICP.MemberIds, slice.Custom1.MemberIds,
                                    slice.Custom2.MemberIds, slice.Custom3.MemberIds, slice.Custom4.MemberIds));
                }
                // Monitor progress
                MonitorEAExtract(output);
            }
            catch (Exception ex)
            {
                _log.Fatal(string.Format("Error while extracting data for {0}, Message: {1}", slice, ex.Message), ex);
                throw ex;
            }
            finally {
                // Retrieve log file
                if(logFile != null) {
                    RetrieveLog(logFile);
                }
            }
            return taskId;
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

            output.InitProgress(taskStatus.ToString());
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
                            _log.InfoFormat("Extract Status: {0} complete", lastTaskStatus);
                            output.Operation = taskStatus.ToString();
                            break;
                    }
                    lastTaskStatus = taskStatus;
                }
                return (int)(numComplete / numRecords * 100);
            });
            output.EndProgress();

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
            _log.DebugFormat("Retreving Log File: {0}", logFile);

            FileUtilities.EnsureFileWriteable(logFile);
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
