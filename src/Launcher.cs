using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;

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
        public static extern bool SetConsoleCtrlHandler(Handler handler, bool Add);
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
        public static bool Interrupted = false;



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
            var cmdLine = new CommandLine.Interface(HFMCmd.Resource.Help.Purpose);
            var arg = cmdLine.AddPositionalArgument("CommandOrFile", "The name of the command to execute, or the path to a file containing commands to execute");
            arg.Validation = new Regex("foo|bar");
            arg.OnParse = (key, val) => {
                cmdLine.AddPositionalArgument("ArgTwo", "This is argument two");
            };
            cmdLine.AddKeywordArgument("UserId", "The user id to use to connect to HFM");
            cmdLine.AddKeywordArgument("Password", "The password to use to connect to HFM");
            cmdLine.AddKeywordArgument("Host", "The HFM cluster or server to connect to");
            cmdLine.AddKeywordArgument("App", "The HFM application to connect to");
            cmdLine.AddFlagArgument("Debug", "Enable debug logging");
            cmdLine.Parse(Environment.GetCommandLineArgs());

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
            Interrupted = true;

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
