using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;

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
        /// Reference to the console output
        private ConsoleOutput _console;
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
            int rc = 0;

            // Define command-line UI
            _cmdLine = new UI(HFMCmd.Resource.Help.Instructions);
            _console = new ConsoleOutput(_cmdLine);

            ConfigureLogging();

            // Register commands
            _log.Fine("Loading available commands...");
            _commands = new Registry();
            _commands.RegisterNamespace("HFMCmd");
            _commands.RegisterNamespace("HFM");

            // Create a Context for invoking Commands
            _context = new Context(_commands, PromptForMissingArg);
            _context.Set(this);
            _context.Set(_console);

            SetupCommandLine();
            rc = ProcessCommandLine();
            if(rc == 0) {
                _log.Info("Exiting with status code 0");
            }
            else {
                _log.FatalFormat("Exiting with status code {0}", rc);
            }
            System.Environment.Exit(rc);
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
            ConsoleAppender ca = new HFMCmd.ConsoleAppender(_console);
            ca.Layout = new log4net.Layout.PatternLayout("%-5level  %message%newline%exception");
            ca.ActivateOptions();
            log4net.Config.BasicConfigurator.Configure(ca);

            // Configure exception renderers
            _logHierarchy.RendererMap.Put(typeof(Exception), new ExceptionMessageRenderer());

            // Set default log level
            _logHierarchy.Root.Level = _logRepository.LevelMap["INFO"];
        }


        // Defines the standard command-line arguments
        protected void SetupCommandLine()
        {
            ValueArgument arg = _cmdLine.AddPositionalArgument("CommandOrFile",
                    "The name of the command to execute, or the path to a file containing commands to execute");
            arg.IsRequired = true;
            arg.Validate = ValidateCommand;

            arg = _cmdLine.AddKeywordArgument("LogLevel", "Set logging to the specified log level",
                    (_, val) => Log(val, null));
            arg.AddValidator(new ListValidator("NONE", "SEVERE", "ERROR", "WARN",
                    "INFO", "FINE", "TRACE", "DEBUG"));

            _cmdLine.AddFlagArgument("Debug", "Enable debug logging",
                    (_, val) => Log("DEBUG", null));
        }


        /// <summary>
        /// Validates the first positional parameter, verifying it is either
        /// the path to a command file, or one of the registered commands.
        /// If it is a command, we dynamically update our command-line definition
        /// to include any additional parameters needed by the specified command.
        /// </summary>
        protected bool ValidateCommand(Argument arg, string argVal, out string errorMsg)
        {
            bool ok;
            errorMsg = String.Format("Command file '{0}' not found", argVal);
            if(_commands.Contains(argVal)) {
                ok = true;

                // Mark the argument as a command
                ((PositionalArgument)arg).IsCommand = true;

                // Add command arguments as keyword args
                Command.Command cmd = _commands[argVal];

                // First, add any args for commands that must be invoked before the
                // requested command, such as SetLogonInfo, OpenApplication, etc
                foreach(var i in _context.FindPathToType(cmd.Type, cmd)) {
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
                if(param.IsSettingsCollection) {
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
                if(setting is DynamicSettingAttribute) {
                    // Instruct parser to include unrecognised args, since these
                    // may correspond to dynamic setting values
                    _cmdLine.Definition.IncludeUnrecognisedKeywordArgs = true;
                    _cmdLine.Definition.IncludeUnrecognisedFlagArgs = true;
                }
                else {
                    var key = setting.Name.Capitalize();
                    if(_cmdLine.Definition[key] == null) {
                        _log.DebugFormat("Adding command-line arg {0}", key);

                        // Add a keyword argument for this setting
                        ValueArgument arg = setting.HasUda("PositionalArg") ?
                            (ValueArgument)_cmdLine.AddPositionalArgument(key, setting.Description) :
                            (ValueArgument)_cmdLine.AddKeywordArgument(key, setting.Description);
                        arg.Alias = setting.Alias;
                        // TODO: Argument should only be optional if prompting is valid (i.e. not headless)
                        arg.IsRequired = false;
                        // arg.IsRequired = !setting.HasDefaultValue;
                        arg.IsSensitive = setting.IsSensitive;
                        if(setting.HasDefaultValue && setting.DefaultValue != null) {
                            arg.DefaultValue = setting.DefaultValue.ToString();
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Prompts for a value for setting
        /// </summary>
        public object PromptForMissingArg(ISetting setting)
        {
            var prompt = string.Format("Enter a value for {0} ({1}): ",
                    setting.Name.Capitalize(), setting.Description);
            return setting.IsSensitive ? _cmdLine.ReadPassword(prompt) :
                                   _cmdLine.ReadLine(prompt);
        }


        /// Process command-line arguments
        protected int ProcessCommandLine()
        {
            int rc = 0;
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
                        rc = 1;
                    }
                }
            }
            catch(ParseException ex) {
                _log.Error(ex);
                rc = 99;
            }
            return rc;
        }


        /// Invokes the specified command, passing in the supplied argument values.
        protected bool InvokeCommand(string command, Dictionary<string, object> args)
        {
            bool ok = true;
            try {
                _context.Invoke(command, args);
            }
            catch(Exception ex) {
                _log.Error(string.Format("An error occurred while attempting to invoke command {0}:",
                           _commands[command].Name), ex);
                ok = false;
            }
            return ok;
        }


        protected bool ProcessCommandFile(string fileName, Dictionary<string, object> args)
        {
            bool ok = true;
            _log.InfoFormat("Processing command file {0}", fileName);
            YAML.Parser parser = new YAML.Parser();
            YAML.Node root = parser.ParseFile(fileName, args);
            Console.WriteLine(root);
            foreach(var cmdNode in root) {
                if(_commands.Contains(cmdNode.Key)) {
                    var cmdArgs = cmdNode.ToDictionary();
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
                // Display a list of commands in two columns
                _log.Info("Displaying available commands:");
                output.SetHeader("Commands", 40, "", 40);
                var cmds = _commands.EachCommand().ToArray();
                int limit = cmds.Length / 2;
                if(cmds.Length % 2 == 1) { limit++; }
                for(var i = 0; i < limit; ++i) {
                    if(limit + i < cmds.Length) {
                        output.WriteRecord(cmds[i], cmds[limit + i]);
                    }
                    else {
                        output.WriteRecord(cmds[i], "");
                    }
                }
                output.End(true);
                output.WriteSingleValue(string.Format("For detailed help on any of the above commands, " +
                                        "use the command '{0} Help <CommandName>'", ApplicationInfo.ExeName));
            }
            else {
                // Display help for the requested command
                var cmd = _commands[command];
                if(cmd.Description != null) {
                    output.WriteSingleValue(cmd.Description, "Description");
                    output.WriteLine();
                }
                if(cmd.NumUserSuppliedParameters > 0) {
                    output.SetHeader("Parameter", 30, "Default Value", 15, "Description");
                    foreach(var parm in cmd.Parameters) {
                        if(parm.HasParameterAttribute) {
                            OutputSetting(output, parm);
                        }
                        else if(parm.IsSettingsCollection) {
                            foreach(var setting in _commands.GetSettings(parm.ParameterType)) {
                                OutputSetting(output, setting);
                            }
                        }
                    }
                    output.End(true);
                }
            }
        }


        private void OutputSetting(IOutput output, ISetting setting)
        {
            if(!setting.IsVersioned || setting.IsCurrent(HFM.HFM.Version)) {
                var name = setting.Name.Capitalize();
                var def = setting.DefaultValue;
                var desc = setting.Description;
                if(!desc.EndsWith(".")) {
                    desc = desc + ".";
                }
                output.WriteRecord(name, def, desc);
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
            if(logFile != null) {
                // Logging needs to respect working directory
                if(!Path.IsPathRooted(logFile)) {
                    _log.Debug("Converting log file to respect working directory");
                    logFile = Environment.CurrentDirectory + @"\" + logFile;
                }

                // Determine if there is already a FileAppender active
                FileAppender fa = (FileAppender)Array.Find<IAppender>(_logRepository.GetAppenders(),
                    (appender) => appender is FileAppender);
                if(fa == null) {
                    fa = new log4net.Appender.FileAppender();
                }

                _log.Info("Logging to file " + logFile);
                fa.Layout = new log4net.Layout.PatternLayout(
                    "%date{yyyy-MM-dd HH:mm:ss} %-5level %logger - %message%newline%exception");
                fa.File = logFile;
                fa.ActivateOptions();
                log4net.Config.BasicConfigurator.Configure(fa);

                // TODO: Add log output for capturing output to log
                //_context.Set(new LogOutput());
            }
        }

    }

}
