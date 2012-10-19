using System;
using System.Threading;

using log4net;


namespace HFMCmd
{

    /// <summary>
    /// Utility class that can be used to monitor the progress of long-running
    /// operations via a separate thread. Supports both synchronous and
    /// asyncrhonous modes of operation (i.e. where the long-running operation
    /// is blocking or non-blocking).
    /// </summary>
    public class ProgressMonitor
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Interval (in milliseconds) for which we will sleep between updating
        // the IOutput instance on progress.
        protected static int SLEEP_INTERVAL = 500;

        // Interval (in seconds) between polling progress function
        public int PollingInterval = 1;

        // Reference to the IOutput used to display the progress status
        protected IOutput _output;
        // Operation to be monitored
        protected string _operation;
        // Total number of steps in operation
        protected int _total;
        // Thread spun up to monitor blocking operations
        protected Thread _monitorThread;


        public string Operation {
            get { return _operation; }
            set {
                _operation = value;
                _output.Operation = _operation;
            }
        }


        /// <summary>
        /// Define a delegate for a function that will return the current
        /// progress of an operation.
        /// </summary>
        public delegate int GetProgress(bool cancel, out bool isRunning);


        /// <summary>
        /// Constructs a progress monitor for tracking the progress of a long-
        /// running operation and updating the progress status via an IOutput
        /// instance. Progress will be measured in percentage terms.
        /// </summary>
        public ProgressMonitor(IOutput output)
            : this(output, "Operation in progress", 100)
        {
        }


        /// <summary>
        /// Constructs a progress monitor for tracking the progress of a long-
        /// running operation and updating the progress status via an IOutput
        /// instance. Progress will be measured in percentage terms.
        /// </summary>
        public ProgressMonitor(IOutput output, string operation)
            : this(output, operation, 100)
        {
        }


        /// <summary>
        /// Constructs a progress monitor for tracking the progress of a long-
        /// running operation and updating the progress status via an IOutput
        /// instance.
        /// </summary>
        public ProgressMonitor(IOutput output, string operation, int total)
        {
            _output = output;
            _operation = operation;
            _total = total;
        }


        /// <summary>
        /// Monitors progress of an operation executing asynchronously in
        /// another thread.
        /// The supplied callback is used to determine the completion progress
        /// of the operation, and whether or not it is still running.
        /// This method blocks until the callback sets the isRunning parameter
        /// to false, or the IOutput instance indicates the operation should be
        /// cancelled, e.g. because the user hits the Escape key.
        /// For a non-blocking alternative, use MonitorProgressAsync.
        /// </summary>
        public void MonitorProgress(GetProgress progressFn)
        {
            int progress = 0;
            DateTime lastPoll = DateTime.MinValue;
            bool cancel = false;
            bool cancelNotified = false;
            bool isRunning = true;

            _output.InitProgress(_operation, _total);

            do {
                // Sleep for half a second
                // Note: the initial delay before displaying a progress bar the first time is deliberate
                // It allows time for the task thread to log messages as it starts it's work
                Thread.Sleep(SLEEP_INTERVAL);

                // Determine progress
                if (lastPoll == null || DateTime.Now.AddSeconds(-PollingInterval) > lastPoll) {
                    progress = progressFn(cancel, out isRunning);
                    cancelNotified = cancelNotified || cancel;
                    lastPoll = DateTime.Now;
                }

                // Update progress status
                cancel = _output.SetProgress(progress);
            }
            while(isRunning && !cancelNotified);

            _output.EndProgress();

            if(cancel) {
                _log.InfoFormat("{0} cancelled", _operation);
            }
        }


        /// <summary>
        /// Monitors progress of a synchronously executing (i.e. blocking)
        /// operation.
        /// This method spins up a background thread to poll the operation
        /// status, and so it returns immediately. Use this method when you want
        /// to monitor the progress of a long-running, blocking operation. Be
        /// sure to call this method immediately prior to invoking the operation.
        /// </summary>
        public void MonitorProgressAsync(GetProgress progressFn)
        {
            _monitorThread = new System.Threading.Thread(() => {
                _log.Trace("Starting progress monitor thread");
                MonitorProgress(progressFn);
                _log.Trace("Progress monitor thread finished");
            });
            _monitorThread.Start();
        }


        /// <summary>
        /// When an async operation is complete, this method should be called
        /// so that the main thread waits for the monitor thread to end and
        /// clean up the progress display.
        /// </summary>
        public void AsyncComplete()
        {
            _monitorThread.Join();
            _monitorThread = null;
        }
    }

}
