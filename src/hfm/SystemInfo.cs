using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using log4net;

using HFMCONSTANTSLib;
#if !LATE_BIND
using HSVSESSIONLib;
using HSVSYSTEMINFOLib;
#endif

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Enumeration defining the different task types.
    /// </summary>
    public enum ETaskType
    {
        Allocate                = tagUSERACTIVITYCODE.USERACTIVITYCODE_ALLOCATE,
        AttachDocument          = tagUSERACTIVITYCODE.USERACTIVITYCODE_ATTACH_DOCUMENT,
#if !HFM_9_3_1
        CalculateEPU            = tagUSERACTIVITYCODE.USERACTIVITYCODE_CALCULATE_EPU,
#endif
        ChartLogic              = tagUSERACTIVITYCODE.USERACTIVITYCODE_CHART_LOGIC,
        Consolidation           = tagUSERACTIVITYCODE.USERACTIVITYCODE_CONSOLIDATION,
        CustomLogic             = tagUSERACTIVITYCODE.USERACTIVITYCODE_CUSTOM_LOGIC,
        DataAuditPurged         = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_AUDIT_PURGED,
        DataClear               = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_CLEAR,
        DataCopy                = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_COPY,
        DataDeleteInvalid       = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_DELETE_INVALID_RECORDS,
        DataEntry               = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_ENTRY,
        DataExtract             = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_EXTRACT,
        DataExtractHAL          = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_EXTRACT_HAL,
        DataLoad                = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_LOAD,
        DataRetrieval           = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_RETRIEVAL,
        DataScan                = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_SCAN,
        DeleteApplication       = tagUSERACTIVITYCODE.USERACTIVITYCODE_APPLICATION_DELETION,
        DetachDocument          = tagUSERACTIVITYCODE.USERACTIVITYCODE_DETACH_DOCUMENT,
        EADelete                = tagUSERACTIVITYCODE.USERACTIVITYCODE_EA_DELETE,
        EAExport                = tagUSERACTIVITYCODE.USERACTIVITYCODE_EA_EXPORT,
#if !HFM_9_3_1
        EAExportFlatFile        = tagUSERACTIVITYCODE.USERACTIVITYCODE_EA_EXPORT_FLATFILE,
#endif
        External                = tagUSERACTIVITYCODE.USERACTIVITYCODE_EXTERNAL,
        ICAutoMatchByAcct       = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_AUTOMATCHBYACCT,
        ICAutoMatchByID         = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_AUTOMATCHBYID,
        ICCreateTransactions    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_CREATE_TRANSACTIONS,
        ICDeleteAll             = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_DELETEALL,
        ICDeleteTransactions    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_DELETE_TRANSACTIONS,
        ICEditTransactions      = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_EDIT_TRANSACTIONS,
        ICLockUnlockEntities    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_LOCKUNLOCK_ENTITIES,
        ICManagePeriods         = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_MANAGE_PERIODS,
        ICManageReasonCodes     = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_MANAGE_REASONCODES,
        ICManualMatchTransactions = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_MANUALMATCH_TRANSACTIONS,
        ICMatchingReportByAcct  = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_MATCHINGRPTBYACCT,
        ICMatchingReportByID    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_MATCHINGRPTBYID,
        ICPostAll               = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_POSTALL,
        ICPostTransactions      = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_POST_TRANSACTIONS,
        ICTransactionReport     = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_TRANSACTIONRPT,
        ICTransactionsExtract   = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_TRANSACTIONS_EXTRACT,
        ICTransactionsLoad      = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_TRANSACTIONS_LOAD,
        ICUnmatchAll            = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_UNMATCHALL,
        ICUnmatchTransactions   = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_UNMATCH_TRANSACTIONS,
        ICUnpostAll             = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_UNPOSTALL,
        ICUnpostTransactions    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IC_UNPOST_TRANSACTIONS,
        Idle                    = tagUSERACTIVITYCODE.USERACTIVITYCODE_IDLE,
        JournalEntry            = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_ENTRY,
        JournalPost             = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_POSTING,
#if HFM_11_1_2_2
        JournalReport           = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_REPORT,
#endif
        JournalRetrieve         = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_RETRIEVAL,
        JournalTemplateEntry    = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_TEMPLATE_ENTRY,
        JournalUnpost           = tagUSERACTIVITYCODE.USERACTIVITYCODE_JOURNAL_UNPOSTING,
        Logoff                  = tagUSERACTIVITYCODE.USERACTIVITYCODE_LOGOFF,
        Logon                   = tagUSERACTIVITYCODE.USERACTIVITYCODE_LOGON,
        LogonFailure            = tagUSERACTIVITYCODE.USERACTIVITYCODE_LOGON_FAILURE,
        MemberListExtract       = tagUSERACTIVITYCODE.USERACTIVITYCODE_MEMBER_LIST_EXTRACT,
        MemberListLoad          = tagUSERACTIVITYCODE.USERACTIVITYCODE_MEMBER_LIST_LOAD,
        MemberListScan          = tagUSERACTIVITYCODE.USERACTIVITYCODE_MEMBER_LIST_SCAN,
        MetadataExtract         = tagUSERACTIVITYCODE.USERACTIVITYCODE_METADATA_EXTRACT,
        MetadataLoad            = tagUSERACTIVITYCODE.USERACTIVITYCODE_METADATA_LOAD,
        MetadataScan            = tagUSERACTIVITYCODE.USERACTIVITYCODE_METADATA_SCAN,
        RulesExtract            = tagUSERACTIVITYCODE.USERACTIVITYCODE_RULES_EXTRACT,
        RulesLoad               = tagUSERACTIVITYCODE.USERACTIVITYCODE_RULES_LOAD,
        RulesScan               = tagUSERACTIVITYCODE.USERACTIVITYCODE_RULES_SCAN,
#if HFM_11_1_2_2
        SystemMatchingReport    = tagUSERACTIVITYCODE.USERACTIVITYCODE_SYSTEM_MATCHING_REPORT,
#endif
        TaskAuditPurged         = tagUSERACTIVITYCODE.USERACTIVITYCODE_TASK_AUDIT_PURGED,
        Translation             = tagUSERACTIVITYCODE.USERACTIVITYCODE_TRANSLATION,
#if !HFM_9_3_1
        URLExtract              = tagUSERACTIVITYCODE.USERACTIVITYCODE_URL_EXTRACT,
        URLLoad                 = tagUSERACTIVITYCODE.USERACTIVITYCODE_URL_LOAD,
        URLScan                 = tagUSERACTIVITYCODE.USERACTIVITYCODE_URL_SCAN,
#endif
        All                     = tagUSERACTIVITYCODE.USERACTIVITYCODE__UBOUND + 1
    }


    /// <summary>
    /// Enumeration defining the different statuses a task may take.
    /// </summary>
    public enum ETaskStatus
    {
        Aborted         = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_ABORTED,
        Completed       = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_COMPLETED,
        NotResponding   = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_NOT_RESPONDING,
        Paused          = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_PAUSED,
        Running         = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_RUNNING,
        ScheduledStart  = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_SCHEDULED_START,
        ScheduledStop   = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_SCHEDULED_STOP,
        Starting        = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_STARTING,
        Stopped         = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_STOPPED,
        Stopping        = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_STOPPING,
        Unknown         = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_UNDEFINED,
        All             = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS__UBOUND + 1
    }



    /// <summary>
    /// A structure for returning information about a task.
    /// </summary>
    public class TaskInfo
    {
        public int TaskId;
        public string User;
        public string Server;
        public ETaskType TaskType;
        public ETaskStatus TaskStatus;
        public int PercentageComplete;
        public DateTime ScheduledStartTime;
        public DateTime ActualStartTime;
        public DateTime LastUpdate;
        public string Description;
        public string LogFile;
    }



    /// <summary>
    /// Wraps an IHsvSystemInfo COM object
    /// </summary>
    public class SystemInfo
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

#if LATE_BIND
        internal readonly dynamic HsvSystemInfo;
#else
        internal readonly HsvSystemInfo HsvSystemInfo;
#endif

        private ProgressMonitor _progressMonitor;


        /// Constructor
        public SystemInfo(Session session)
        {
            _log.Trace("Constructing SystemInfo object");
#if LATE_BIND
            HsvSystemInfo = session.HsvSession.SystemInfo;
#else
            HsvSystemInfo = (HsvSystemInfo)session.HsvSession.SystemInfo;
#endif
        }


        /// <summary>
        /// Returns a list containing details for tasks that match the selection
        /// criteria.
        /// </summary>
        protected List<TaskInfo> GetTasks(ETaskType taskType, ETaskStatus taskStatus,
                string user, string server, bool currentSessionOnly)
        {
            int userId = 0, totalTasks = 0;
            object oTaskIds = null, oTaskTypes = null, oTaskProgress = null, oTaskStatuses = null,
                   oUsers = null, oServers = null,
                   oScheduledStartTimes = null, oActualStartTimes = null, oLastUpdateTimes = null,
                   oDescriptions = null, oLogFiles = null;
            int[] taskIds, progress;
            ETaskType[] taskTypes;
            ETaskStatus[] taskStatuses;
            double[] scheduledTimes, actualTimes, lastUpdateTimes;
            string[] users, servers, descriptions, logFiles;

            if(user != null) {
                HFM.Try("Retrieving user activity id for {0}", user,
                        () => HsvSystemInfo.GetActivityUserID(user, out userId));
            }

            HFM.Try("Retrieving details of tasks",
                () => HsvSystemInfo.EnumRunningTasks(taskType == ETaskType.All, (int)taskType,
                    user == null, userId, server == null, server,
                    !currentSessionOnly, taskStatus == ETaskStatus.All, (int)taskStatus,
                    0, 10000, // Retrieve the first 10,000 records
                    out oTaskIds, out oTaskTypes, out oTaskProgress, out oTaskStatuses,
                    out oUsers, out oServers, out oScheduledStartTimes, out oActualStartTimes,
                    out oLastUpdateTimes, out oDescriptions, out oLogFiles, out totalTasks));

            // Note: We can't use HFM.Object2Array for the primitive and ennum
            // types here, since the returned values are not object[]!?
            // May have something to do with the params being declared out, rather
            // than in/out?
            taskIds = (int[])oTaskIds;
            taskTypes = (ETaskType[])oTaskTypes;
            progress = (int[])oTaskProgress;
            taskStatuses = (ETaskStatus[])oTaskStatuses;
            users = HFM.Object2Array<string>(oUsers);
            servers = HFM.Object2Array<string>(oServers);
            scheduledTimes = (double[])oScheduledStartTimes;
            actualTimes = (double[])oActualStartTimes;
            lastUpdateTimes = (double[])oLastUpdateTimes;
            descriptions = HFM.Object2Array<string>(oDescriptions);
            logFiles = HFM.Object2Array<string>(oLogFiles);

            var tasks = new List<TaskInfo>(totalTasks);

            for(var i = 0; taskIds != null && i < taskIds.Length; ++i) {
                tasks.Add(new TaskInfo() {
                    TaskId = taskIds[i],
                    User = users[i],
                    Server = servers[i],
                    TaskType = taskTypes[i],
                    TaskStatus = taskStatuses[i],
                    PercentageComplete = progress[i],
                    ScheduledStartTime = DateTime.FromOADate(scheduledTimes[i]),
                    ActualStartTime = DateTime.FromOADate(actualTimes[i]),
                    LastUpdate = DateTime.FromOADate(lastUpdateTimes[i]),
                    Description = descriptions[i],
                    LogFile = logFiles[i]
                });
            }

            return tasks;
        }


        [Command("Returns a list of tasks that match the specified filter criteria")]
        public List<TaskInfo> EnumTasks(
                [Parameter("Filter tasks to the a particular type",
                 DefaultValue = ETaskType.All)]
                ETaskType taskTypeFilter,
                [Parameter("Filter tasks to the a particular status",
                 DefaultValue = ETaskStatus.All)]
                ETaskStatus taskStatusFilter,
                [Parameter("User name to filter by (blank for all)",
                 DefaultValue = null)]
                string taskUserName,
                [Parameter("Server name to filter by (blank for all servers in cluster)",
                 DefaultValue = null)]
                string taskServerName,
                IOutput output)
        {
            var tasks = GetTasks(taskTypeFilter, taskStatusFilter, taskUserName, taskServerName, false);

            output.SetHeader("Task Id", 12, "User Id", "Server", "Task Type", "Task Status", 14,
                    "% Complete", 10, "Description");
            foreach(var task in tasks) {
                output.WriteRecord(task.TaskId, task.User, task.Server, task.TaskType,
                        task.TaskStatus, task.PercentageComplete, task.Description);
            }
            output.End();

            return tasks;
        }


        /// Returns the task id of the currently running task
        protected TaskInfo GetRunningTask()
        {
            return GetTasks(ETaskType.All, ETaskStatus.Running, null, null, true).FirstOrDefault();
        }


        /// Updates the percentage completion, status, etc of the specified task
        protected int GetTaskProgress(TaskInfo task)
        {
            int progress = 0, status = 0;
            double lastUpdate = 0;
            string desc = null;

            HFM.Try("Retrieving task progress",
                    () => HsvSystemInfo.GetRunningTaskProgress(task.TaskId, out progress,
                                     out status, out lastUpdate, out desc));
            task.TaskStatus = (ETaskStatus)status;
            task.PercentageComplete = progress;
            task.LastUpdate = DateTime.FromOADate(lastUpdate);
            task.Description = desc;

            return progress;
        }


        /// Cancels a running task
        protected void CancelRunningTask(int taskId)
        {
            HFM.Try("Cancelling running task {0}", taskId,
                    () => HsvSystemInfo.StopRunningTask(taskId));
        }


        /// <summary>
        /// Monitors the progress (via a separate monitor thread) of a blocking
        /// API call that is about to be run on the main thread. The query to
        /// get a handle to the blocking task will not be performed for half a
        /// second, giving the caller time to initiate the blocking task after
        /// calling this method.
        /// Note: When the blocking task returns, the caller should immediately
        /// call the TaskComplete method to wait for the monitor thread to
        /// complete. Otherwise progress updates can be intermingled with log
        /// output.
        /// </summary>
        public void MonitorBlockingTask(IOutput output)
        {
            TaskInfo task = null;

            _progressMonitor = new ProgressMonitor(output);
            _progressMonitor.MonitorProgressAsync(delegate(bool cancel, out bool isRunning) {
                int progress;

                if(task == null) {
                    // First time through (half a second later), so get task
                    // which should now be running
                    task = GetRunningTask();
                    if(task != null && output.Operation == null) {
                        output.Operation = task.TaskType.ToString();
                    }
                }

                if(task != null) {
                    // Check on the task progress
                    progress = GetTaskProgress(task);
                    isRunning = task.TaskStatus == ETaskStatus.Running;
                    if(cancel && isRunning) {
                        CancelRunningTask(task.TaskId);
                    }
                }
                else {
                    // No task is running - either it finished already, or there
                    // was an error launching it; either way, indicate we are done
                    isRunning = false;
                    progress = 100;
                }
                return progress;
            });
        }


        /// <summary>
        /// When a blocking API call has completed that has been monitored via
        /// MonitorBlockingTask, this method should be called so as to wait for
        /// monitor thread to complete before moving onto other things. E.g.
        ///     MonitorBlockingTask(output);
        ///     ... call to blocking API here ...
        ///     BlockingTaskComplete();
        /// </summary>
        public void BlockingTaskComplete()
        {
            _progressMonitor.AsyncComplete();
            _progressMonitor = null;
        }


        [Command("Waits for the specified task to complete")]
        public void WaitForTask(
                [Parameter("The task id to wait for")]
                int taskId,
                IOutput output)
        {
            TaskInfo task = GetTasks(ETaskType.All, ETaskStatus.Running, null, null, false).
                                FirstOrDefault(t => t.TaskId == taskId);

            if(task != null) {
                var pm = new ProgressMonitor(output);
                pm.MonitorProgress((bool cancel, out bool isRunning) => {
                    int progress;

                    progress = GetTaskProgress(task);
                    isRunning = task.TaskStatus == ETaskStatus.Running;
                    if(cancel && isRunning) {
                        CancelRunningTask(task.TaskId);
                    }

                    return progress;
                });
                output.IterationComplete();
            }
            else {
                _log.InfoFormat("No running task was found with task id {0}", taskId);
            }
        }

    }

}
