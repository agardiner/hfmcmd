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
using Encryption;
using Utilities;


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
            var cmdLineArgs = Environment.GetCommandLineArgs();

            // Define command-line UI
            _cmdLine = new UI(HFMCmd.Resource.Help.Instructions);
            _console = new ConsoleOutput(_cmdLine);

            ConfigureLogging(cmdLineArgs);

            // Register commands
            _log.Fine("Loading available commands...");
            _commands = new Registry();
            _commands.RegisterNamespace("HFMCmd");
            _commands.RegisterNamespace("HFM");

            // Create a Context for invoking Commands
            _context = new Context(_commands, PromptForMissingArg);
            _context.Set(this);
            _context.Set(_console);
            _context.Verify();

            if(cmdLineArgs.Length > 1) {
                SetupCommandLine();
                rc = ProcessCommandLine(cmdLineArgs);
            }
            else {
                StartREPL();
            }

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
        protected void ConfigureLogging(IEnumerable<string> cmdLineArgs)
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
            if(cmdLineArgs.Contains("--debugStartup")) {
                _logHierarchy.Root.Level = _logRepository.LevelMap["DEBUG"];
            }
            else {
                _logHierarchy.Root.Level = _logRepository.LevelMap["INFO"];
            }
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
                    var logArg = _cmdLine.AddPositionalArgument("LogFile", "Path to log file",
                                    (_, logFile) => Log(null, logFile));
                    logArg.IsRequired = false;
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
        protected int ProcessCommandLine(IEnumerable<string> cmdLineArgs)
        {
            int rc = 0;
            try {
                var args = _cmdLine.Parse(cmdLineArgs);
                if(args != null) {
                    DecryptValues(args);
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


        private void DecryptValues(Dictionary<string, object> args)
        {
            var keys = args.Keys.ToArray();
            foreach(var key in keys) {
                var val = args[key] as string;
                if(val != null && val.StartsWith("!")) {
                    args[key] = DecryptValue(val);
                }
            }
        }


        public string DecryptValue(string cipherText)
        {
            string plainText;
            if(cipherText.StartsWith("!AES")) {
                plainText = AES.Decrypt(cipherText.Substring(4).Replace('!', '='));
            }
            else if(cipherText.StartsWith("!WPD")) {
                plainText = WindowsProtectedData.Decrypt(cipherText.Substring(4).Replace('!', '='));
            }
            else {
                plainText = cipherText;
            }
            return plainText;
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
            foreach(var cmdNode in root) {
                if(_commands.Contains(cmdNode.Key)) {
                    var cmdArgs = cmdNode.ToDictionary();
                    DecryptValues(cmdArgs);
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


        /// Launches in REPL mode
        protected void StartREPL()
        {
            string input = null;
            ValueArgument cmdArg = new PositionalArgument() {
                Key = "Command",
                Description = "The name of the command to execute, or Quit to exit",
                IsRequired = true,
                Validate = ValidateCommand
            };

            while(true) {
                input = _cmdLine.ReadLine("hfm> ");

                if(UI.Interrupted ||
                   String.Compare(input, "exit", StringComparison.OrdinalIgnoreCase) == 0 ||
                   String.Compare(input, "quit", StringComparison.OrdinalIgnoreCase) == 0) {
                    break;
                }

                _cmdLine.Definition.Clear();
                SetupCommandLine();
                ProcessCommandLine(("hfm " + input).SplitSpaces());
            }
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
                var desc = cmd.Description;
                if(desc != null) {
                    if(!desc.EndsWith(".")) { desc = desc + "."; }
                    output.WriteSingleValue(desc, "Description");
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


        [Command("Encrypts the supplied value using either 256-bit AES encryption or " +
                 "Windows Protected Data. " +
                 "AES is a highly secure symmetric cipher approved by the NSA, but it " +
                 "requires the use of an encryption key (in addition to the plain or " +
                 "encrypted text) for both encryption and decryption. A random encryption " +
                 "key will therefore be generated and saved to a file the first time this " +
                 "command is used to generate an AES encrypted value. This encryption key " +
                 "can be copied to other machines if the same encrypted password needs to " +
                 "be used on multiple machines." +
                 "Windows Protected Data is also highly secure, but uses a secret encryption " +
                 "key specific to a single machine. This means the encrypted password is not " +
                 "portable; it can only be decrypted on the same machine where it was encrypted. " +
                 "For maximum security, use non-portable Windows Protected Data, or use AES but " +
                 "do NOT store the encrypted password on the same machine as the encryption key.")]
        public void EncryptPassword(
                [Parameter("The value to be encrypted",
                           IsSensitive = true, Uda = "PositionalArg")]
                string plainText,
                [Parameter("Flag indicating whether encrypted password should be decryptable " +
                           "on other machines. Set to true if you want to be able to use the same " +
                           "encrypted password on multiple machines, or false if the encrypted " +
                           "value will only be used on this machine.",
                           DefaultValue = false)]
                bool portable)
        {
            string cipherText;
            if(portable) {
                // We replace the = from the base-64 encoded value with !, since
                // the appearance of = can cause a problem on command lines (where
                // it looks like a variable assignment). As ! is not part of the
                // base-64 encoding, it won't appear anywhere else.
                cipherText = "!AES" + AES.Encrypt(plainText).Replace('=', '!');
                _log.InfoFormat("If you intend to use the same encrypted password on other machines, " +
                          "you must also copy the encryption key file {0} to the {1} directory on " +
                          "each machine.", AES.EncryptionKeyFile, ApplicationInfo.ExeName);
            }
            else {
                cipherText = "!WPD" + WindowsProtectedData.Encrypt(plainText).Replace('=', '!');
            }
            _log.InfoFormat("Encrypted value: {0}", cipherText);
        }


#if DebugEncryption
        [Command("Decrypts the supplied value")]
        public void DecryptPassword(
                [Parameter("The value to be decrypted",
                           Uda = "PositionalArg")]
                string cipherText)
        {
            string plainText = DecryptValue(cipherText);
            _log.InfoFormat("Decrypted value: {0}", plainText);
        }
#endif

    }

}
