using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
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
    /// Main class used to launch the application.
    /// </summary>
    public class Launcher
    {

        /// <summary>
        /// Main program entry point
        /// </summary>
        public static void Main()
        {
            // Register handler for Ctrl-C etc
            var hr = UI.RegisterCtrlHandler();

            // Run the application
            new Application().Run();

            // This line needs to appear at the end of the prgram code as a marker to
            // the GC so that it does not collect our control-key handler
            GC.KeepAlive(hr);
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

        /// Registry of available commands
        private Registry _commands;
        /// Current context in which to execute commands
        private Context _context;
        /// Reference to the command-line interface/parser
        private UI _cmdLine;
        /// Reference to the argument mapper used to convert command-line args
        /// to objects
        private PluggableArgumentMapper _argumentMapper;
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

            // TODO: Add means to get/process flag args from command-line before doing anything else
            //_logHierarchy.Root.Level = _logRepository.LevelMap["DEBUG"];

            // Register commands
            _log.Fine("Loading available commands...");
            _commands = new Registry();
            _commands.RegisterNamespace("HFMCmd");
            _commands.RegisterNamespace("HFM");

            // Define command-line UI
            _argumentMapper = new PluggableArgumentMapper();
            _cmdLine = new UI(HFMCmd.Resource.Help.Purpose, _argumentMapper);

            // Create a Context for invoking Commands
            _context = new Context(_commands);
            _context.Set(this);
            //_context.Set(new LogOutput());
            _context.Set(new ConsoleOutput(_cmdLine));

            // Standard command-line arguments
            ValueArgument arg = _cmdLine.AddPositionalArgument("CommandOrFile",
                    "The name of the command to execute, or the path to a file containing commands to execute");
            arg.IsRequired = true;
            arg.Validate = ValidateCommand;
            arg = _cmdLine.AddKeywordArgument("LogLevel", "Set logging to the specified level",
                    (key, val) => Log(val, null));
            arg.AddValidator(new ListValidator("NONE", "SEVERE", "ERROR", "WARN",
                    "INFO", "FINE", "TRACE", "DEBUG"));
            _cmdLine.AddFlagArgument("Debug", "Enable debug logging",
                    (key, val) => Log("DEBUG", null));

            // Process command-line arguments
            try {
                var args = _cmdLine.Parse(Environment.GetCommandLineArgs());
                if(args != null) {
                    string cmdOrFile = args["CommandOrFile"] as string;
                    bool ok;
                    if(_commands.Contains(cmdOrFile)) {
                        ok = InvokeCommand(cmdOrFile, args);
                    }
                    else {
                        ok = ProcessCommandFile(cmdOrFile, args);
                    }

                    if(!ok) {
                        System.Environment.Exit(1);
                    }
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
                if(ok) {
                    // Instruct parser to include unrecognised args, since these
                    // will be treated as variable assignments
                    _cmdLine.Definition.IncludeUnrecognisedKeywordArgs = true;
                    _cmdLine.Definition.IncludeUnrecognisedFlagArgs = true;
                }
            }
            return ok;
        }


        /// Add additional arguments needed by the command
        protected void AddCommandParamsAsArgs(Command.Command cmd)
        {
            _log.TraceFormat("Adding command-line args for {0} command", cmd.Name);
            foreach(var param in cmd.Parameters) {
                _log.DebugFormat("Processing param {0}", param);
                if(param.IsCollection) {
                    _log.DebugFormat("Processing collection type {0}", param.ParameterType);
                    // Get individual settings from collection and add them
                    foreach(var setting in _commands.GetSettings(param.ParameterType)) {
                        AddSettingAsArg(setting);
                    }
                }
                else if(param.HasParameterAttribute) {
                    AddSettingAsArg(param);
                }
            }
        }


        /// Add a command-line argument for an ISetting
        protected void AddSettingAsArg(ISetting setting)
        {
            if(!setting.IsVersioned || setting.IsCurrent(HFM.HFM.Version)) {
                var key = setting.Name.Capitalize();
                _log.DebugFormat("Adding command-line arg {0}", key);

                // Add a keyword argument for this setting
                ValueArgument arg = setting.HasUda("PositionalArg") ?
                    (ValueArgument)_cmdLine.AddPositionalArgument(key, setting.Description, setting.ParameterType) :
                    (ValueArgument)_cmdLine.AddKeywordArgument(key, setting.Description, setting.ParameterType);

                arg.IsRequired = !setting.HasDefaultValue;
                arg.IsSensitive = setting.IsSensitive;
                if(setting.HasDefaultValue && setting.DefaultValue != null) {
                    arg.DefaultValue = setting.DefaultValue.ToString();
                }
            }
        }


        /// Invokes the specified command, passing in the supplied argument values.
        protected bool InvokeCommand(string command, Dictionary<string, object> args)
        {
            bool ok = true;
            try {
                _context.Invoke(command, args);
            }
            catch(TargetInvocationException) {
                ok = false;
                if(_log.IsDebugEnabled) {
                    throw;
                }
            }
            return ok;
        }


        protected bool ProcessCommandFile(string fileName, Dictionary<string, object> args)
        {
            bool ok = true;
            YAML.Parser parser = new YAML.Parser();
            YAML.Node root = parser.ParseFile(fileName, args);
            Console.WriteLine(root);
            foreach(var cmdNode in root) {
                if(_commands.Contains(cmdNode.Key)) {
                    var cmdArgs = cmdNode.ToDictionary();
                    // TODO: Handle argument mapping
                    ok = ok && InvokeCommand(cmdNode.Key, cmdArgs);
                }
                else {
                    throw new ArgumentException(string.Format("Unknown command '{0}' encountered in command file {1}",
                                cmdNode.Key, fileName));
                }
                if(!ok) {
                    break;
                }
            }
            return ok;
        }



        [Command("Displays help information")]
        public void Help(
                [Parameter("The name of a command for which to display detailed help",
                 DefaultValue = null, Uda = "PositionalArg")]
                string command,
                IOutput output)
        {
            if(output == null) return;
            if(command == null) {
                // Display general help
                output.WriteLine(HFMCmd.Resource.Help.General);
            }
            else if(string.Equals(command, "Commands", StringComparison.OrdinalIgnoreCase)) {
                // Display a list of commands
                output.WriteEnumerable(_commands.EachCommand(), "Commands", 40);
            }
            else {
                // Display help for the requested command
                var cmd = _commands[command];
                output.WriteLine(string.Format("Command: {0}", cmd.Name));
                output.WriteLine();
                if(cmd.Description != null) {
                    output.WriteSingleValue(cmd.Description, "Description", 80);
                    output.WriteLine();
                }
                if(cmd.Parameters != null) {
                    output.SetHeader("Parameter", 30, "Description", 50);
                    foreach(var parm in cmd.Parameters) {
                        if(parm.HasParameterAttribute) {
                            if(!parm.IsVersioned || parm.IsCurrent(HFM.HFM.Version)) {
                                output.WriteRecord(parm.Name.Capitalize(), parm.Description);
                            }
                        }
                        else if(parm.IsCollection) {
                            foreach(var setting in _commands.GetSettings(parm.ParameterType)) {
                                if(!parm.IsVersioned || setting.IsCurrent(HFM.HFM.Version)) {
                                    output.WriteRecord(setting.Name.Capitalize(), setting.Description);
                                }
                            }
                        }
                    }
                    output.End();
                }
            }
        }


        [Command("Set the log level and/or log file")]
        public void Log(
                [Parameter("Level at which to log; unchanged if not specified", DefaultValue = null)]
                string level,
                [Parameter("Path to log file; unchanged if not specified", DefaultValue = null)]
                string logFile)
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
