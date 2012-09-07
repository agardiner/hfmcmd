using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;

using log4net;
using log4net.Appender;
using log4net.Repository;
using log4net.Repository.Hierarchy;

using Command;
using CommandLine;


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

            // Run the application
            new Application().Run();

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

    }


    /// <summary>
    /// Main application class.
    /// </summary>
    public class Application
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Registry _commands;
        private Context _context;
        /// Reference to the command-line interface/parser
        private UI _cmdLine;
        /// Reference to the log4net repository
        private ILoggerRepository _logRepository;
        /// Reference to the logger hierarchy
        private Hierarchy _logHierarchy;


        /// Constructor
        public Application()
        {
        }


        /// <summary>
        /// Main program entry point
        /// </summary>
        public void Run()
        {
            ConfigureLogging();

            // Register commands
            _log.Fine("Loading available commands...");
            _commands = new Registry();
            _commands.RegisterNamespace("HFMCmd");
            _commands.RegisterNamespace("HFM");

            // Create a Context for invoking Commands
            _context = new Context(_commands);
            _context.Set(this);

            // Process command-line arguments
            _cmdLine = new UI(HFMCmd.Resource.Help.Purpose);
            ValueArgument arg = _cmdLine.AddPositionalArgument("CommandOrFile",
                    "The name of the command to execute, or the path to a file containing commands to execute");
            arg.Validate = ValidateCommand;


            // Standard command-line arguments
            arg = _cmdLine.AddKeywordArgument("LogLevel", "Set logging to the specified level",
                    (key, val) => Log(val, null));
            arg.AddValidator(new ListValidator("NONE", "SEVERE", "ERROR", "WARN",
                    "INFO", "FINE", "TRACE", "DEBUG"));
            _cmdLine.AddFlagArgument("Debug", "Enable debug logging",
                    (key, val) => Log("DEBUG", null));

            try {
                var args = _cmdLine.Parse(Environment.GetCommandLineArgs());
                if(args != null) {
                    _context.Invoke(args["CommandOrFile"] as string, args);
                }
            }
            catch(ParseException ex) {
                _log.Error(ex);
            }
        }


        /// <summary>
        /// Configures logging.
        /// </summary>
        protected void ConfigureLogging()
        {
            // Get repository, define custom levels
            _logRepository = LogManager.GetRepository();
            _logHierarchy = _logRepository as Hierarchy;
            _logRepository.LevelMap.Add(new log4net.Core.Level(30000, "FINE"));
            _logRepository.LevelMap.Add(new log4net.Core.Level(20000, "TRACE"));
            _logRepository.LevelMap.Add(new log4net.Core.Level(10000, "DEBUG"));

            // Create a console logger
            ConsoleAppender ca = new ConsoleAppender();
            ca.Layout = new log4net.Layout.PatternLayout(
                "%date{HH:mm:ss} %-5level  %message%newline");
            ca.ActivateOptions();

            // Configure exception renderers
            _logHierarchy.RendererMap.Put(typeof(Exception), new ExceptionMessageRenderer());
            //logHier.RendererMap.Put(typeof(HFM.HFMException), new ExceptionMessageRenderer());

            // Configure log4net to use this, with other default settings
            log4net.Config.BasicConfigurator.Configure(ca);

            // Set default log level
            _logHierarchy.Root.Level = _logRepository.LevelMap["INFO"];
        }


        /// <summary>
        /// Validates the first positional parameter, verifying it is either
        /// the path to a command file, or one of the registered commands.
        /// If it is a command, we dynamically update our command-line definition
        /// to include any additional parameters needed by the specified command.
        /// </summary>
        protected bool ValidateCommand(string argVal, out string errorMsg)
        {
            bool ok;
            errorMsg = String.Format("Command file '{0}' not found", argVal);
            if(_commands.Contains(argVal)) {
                ok = true;

                // Add command arguments as keyword args
                Command.Command cmd = _commands[argVal];

                // First, add any args for commands that must be invoked before the
                // requested command, such as SetLogonInfo, OpenApplication, etc
                foreach(var i in _context.FindPathToType(cmd.Type)) {
                    if(i.IsCommand) {
                        AddCommandParamsAsArgs(i.Command);
                    }
                }

                // Now add arguments for the command requested
                AddCommandParamsAsArgs(cmd);
            }
            else {
                ok = File.Exists(argVal);
            }
            return ok;
        }


        // Add additional arguments needed by the command
        protected void AddCommandParamsAsArgs(Command.Command cmd)
        {
            string key;
            KeywordArgument arg;

            _log.TraceFormat("Adding keyword args for {0} command", cmd.Name);
            foreach(var param in cmd.Parameters) {
                key = char.ToUpper(param.Name[0]) + param.Name.Substring(1);
                _log.DebugFormat("Adding keyword arg {0}", key);
                arg = _cmdLine.AddKeywordArgument(key, param.Description);
                arg.IsRequired = !param.HasDefaultValue;
                if(param.HasDefaultValue && param.DefaultValue != null) {
                    arg.DefaultValue = param.DefaultValue.ToString();
                }
            }
        }


        [Command, Description("Set the log level and/or log file")]
        public void Log([Description("Level at which to log"), DefaultValue(null)] string level,
                        [Description("Path to log file"), DefaultValue(null)] string logFile)
        {
            // Set the log level
            if(level != null) {
                _log.FineFormat("Setting log level to {0}", level.ToUpper());
                _logHierarchy.Root.Level = _logRepository.LevelMap[level.ToUpper()];
            }

            // Set the log file
            if (logFile != null) {
                // Logging needs to respect working directory
                if (Path.IsPathRooted(logFile)) {
                    _log.Debug("Converting log file to respect working directory");
                    logFile = Environment.CurrentDirectory + @"\" + logFile;
                }

                // Determine if there is already a FileAppender active
                FileAppender fa = (FileAppender)Array.Find<IAppender>(_logRepository.GetAppenders(),
                    (appender) => appender is FileAppender);
                if (fa == null) {
                    fa = new log4net.Appender.FileAppender();
                }

                _log.Info("Logging to file " + logFile);
                fa.Layout = new log4net.Layout.PatternLayout(
                    "%date{yyyy-MM-dd HH:mm:ss} %-5level %logger - %message%newline%exception");
                fa.File = logFile;
                fa.ActivateOptions();
                log4net.Config.BasicConfigurator.Configure(fa);
            }
        }


    }

}
