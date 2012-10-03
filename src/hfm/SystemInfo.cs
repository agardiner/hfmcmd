using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;

using HFMCONSTANTSLib;
using HSVSYSTEMINFOLib;

using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Enumeration defining the different statuses a task may take.
    /// </summary>
    public enum ETaskType
    {
        DataLoad = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_LOAD,
        DataExtract = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_EXTRACT,
        DataClear = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_CLEAR,
        DataClearInvalid = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_DELETE_INVALID_RECORDS,
        DataCopy = tagUSERACTIVITYCODE.USERACTIVITYCODE_DATA_COPY,
        MetadataLoad = tagUSERACTIVITYCODE.USERACTIVITYCODE_METADATA_LOAD,
        MetadataExtract = tagUSERACTIVITYCODE.USERACTIVITYCODE_METADATA_EXTRACT
    }


    /// <summary>
    /// Enumeration defining the different statuses a task may take.
    /// </summary>
    public enum ETaskStatus
    {
        Running = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_RUNNING,
        Aborted = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_ABORTED,
        Completed = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_COMPLETED,
        Paused = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_PAUSED,
        ScheduledStart = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_SCHEDULED_START,
        ScheduledStop = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_SCHEDULED_STOP,
        Starting = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_STARTING,
        Stopping = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_STOPPING,
        NotResponding = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_NOT_RESPONDING,
        Undefined = tagUSERACTIVITYSTATUS.USERACTIVITYSTATUS_UNDEFINED
    }



    /// <summary>
    /// A structure for returning information about a task.
    /// </summary>
    public struct TaskInfo
    {
        public int TaskId;
        public string User;
        public string Server;
        public ETaskType TaskType;
        public ETaskStatus TaskStatus;
        public int PercentageComplete;
        public DateTime StartTime;
        public DateTime LastUpdate;
        public string Description;
    }



    /// <summary>
    /// Wraps an IHsvSystemInfo COM object
    /// </summary>
    public class SystemInfo
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected readonly HsvSystemInfo _hsvSystemInfo;


        public SystemInfo(HsvSystemInfo si)
        {
            _hsvSystemInfo = si;
        }


        protected int GetRunningTaskId(ETaskType taskType)
        {
            // TODO: Implement me
            return -1;
        }

        protected int GetRunningTaskProgress(int taskId, out ETaskStatus status)
        {
            // TODO: Implement me
            status = ETaskStatus.Undefined;
            return 0;
        }


        protected void CancelRunningTask(int taskId)
        {
            // TODO: Implement me
        }


        /// <summary>
        /// Monitors a running task via a separate thread.
        /// </summary>
        public void MonitorRunningTaskAsync(IOutput output, ETaskType taskType)
        {
            var _taskId = GetRunningTaskId(taskType);
            var pm = new ProgressMonitor(output, taskType.ToString(), 100);

            pm.MonitorProgressAsync(delegate(bool cancel, out bool isRunning) {
                int progress;
                ETaskStatus status;

                progress = GetRunningTaskProgress(_taskId, out status);
                isRunning = (ETaskStatus)status == ETaskStatus.Running;
                if(cancel && isRunning) {
                    CancelRunningTask(_taskId);
                }

                return progress;
            });
        }

    }

}
