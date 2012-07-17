using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;


namespace HFMCmd
{
    /// <summary>
    /// Enumeration of interrupt events that might be received by the process
    /// via a ConsoleCtrlHandler callback.
    /// </summary>
    public enum EInterruptTypes
    {
        Ctrl_C = 0,
        Ctrl_Break = 1,
        Close = 2,
        Logoff = 5,
        Shutdown = 6
    }


    /// <summary>
    /// Class to hold definition of external SetConsoleCtrlHandler routine.
    /// </summary>
    class Win32
    {
        public delegate bool Handler(EInterruptTypes ctrlType);

        [DllImport("Kernel32")]
        public static extern Boolean SetConsoleCtrlHandler(Handler handler, bool Add);
    }



    /// <summary>
    /// Main class used to launch the application.
    /// </summary>
    public class Launcher
    {

        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Static public flag indicating that the application is to terminate
        /// immediately, e.g. in response to a Ctrl-C or Logoff event. Any long-
        /// running command should check this flag periodically and attempt to
        /// abort gracefully.
        /// </summary>
        public static bool Terminated = false;



        /// <summary>
        /// Main program entry point
        /// </summary>    
        public static void Main()
        {
            // Hook up CtrlHandler to handle breaks, logoffs, etc
            Win32.Handler hr = new Win32.Handler(CtrlHandler);
            Win32.SetConsoleCtrlHandler(hr, true);

            ConfigureLogging();

            // TODO: Register commands
            
            // TODO: Process command-line arguments

            // This line needs to appear at the end of the prgram code as a marker to
            // the GC so that it does not collect our control-key handler
            GC.KeepAlive(hr);
        }


        /// <summary>
        /// Handler to receive control events, such as Ctrl-C and logoff and 
        /// shutdown events. As a minimum, this logs the event, so that a record
        /// of why the process exited is maintained.
        /// </summary>
        /// <param name="ctrlType">The type of event that occurred.</param>
        /// <returns>True, indicating we have handled the event.</returns>
        static bool CtrlHandler(EInterruptTypes ctrlType)
        {
            _log.Warn("An interrupt [" + ctrlType + "] has been received");
            Terminated = true;

            return true;
        }


        /// <summary>
        /// Configures logging.
        /// </summary>
        protected static void ConfigureLogging()
        {
            // Create a console logger
            ConsoleAppender ca = new ConsoleAppender();
            ca.Layout = new log4net.Layout.PatternLayout(
                "%date{HH:mm:ss} %-5level  %message%newline");
            ca.ActivateOptions();
            log4net.Config.BasicConfigurator.Configure(ca);

            Hierarchy logHier = (Hierarchy)LogManager.GetRepository();

            // TODO: Configure exception renderers
            
            // Set log level
            logHier.Root.Level = log4net.Core.Level.Info;
        }

    }
}