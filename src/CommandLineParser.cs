using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// Base class for all types of command-line arguments.
    /// Each argument must have a key that will be used to access the value in
    /// the returned Dictionary, and a description that is shown when help is
    /// displayed.
    public abstract class Argument
    {
        /// Delegate definition for callback after parsing this argument
        public delegate void OnParseHandler(string key, string value);

        /// The key that will identify this argument in the parse result.
        public string Key { get; set; }
        /// The argument description
        public string Description { get; set; }
        /// A callback to be called when the argument has been parsed successfully
        public OnParseHandler OnParse { get; set; }
    }

    /// ValueArguments are arguments that take a value. This is the parent class
    /// for positional and keyword arguments.
    public abstract class ValueArgument : Argument
    {
        /// Whether the argument is required or optional
        public bool IsRequired { get; set; }
        /// The default value for the argument
        public string DefaultValue { get; set; }
        /// A regular expression the argument must satisfy
        public Regex Validation { get; set; }
        /// A flag indicating whether the argument represents a password
        public bool IsPassword { get; set; }
    }

    /// Positional arguments are those where a value is specified without its
    /// key in a certain pos
    public class PositionalArgument : ValueArgument
    {
        public PositionalArgument()
        {
            IsRequired = true;
        }
    }

    /// A keyword argument can appear in any position, since its key tells us
    /// which argument it corresponds to. The key may be specified as a prefix
    /// to the value, i.e. key:value, or as a separate argument preceding the
    /// value.
    public class KeywordArgument : ValueArgument {}

    /// Flag arguments are booleans that are set if the flag is encountered.
    /// A flag argument is identified by a -- or / prefix.
    public class FlagArgument : Argument {}


    /// Class representing a set of Argument definitions.
    public class Definition
    {
        /// Collection of Argument objects, defining the permitted/expected
        /// arguments this program takes.
        internal Dictionary<string, Argument> Arguments = new Dictionary<string, Argument>();
        /// List identifying the insertion order of positional arguments.
        protected List<string> PositionalArgumentOrder = new List<string>();

        /// Purpose of the program whose command-line arguments we are parsing.
        public string Purpose { get; set; }

        /// Returns a list of the positional arguments
        public List<PositionalArgument> PositionalArguments {
            get {
                var query = from arg in Arguments.Values
                            where arg is PositionalArgument
                            select arg as PositionalArgument;
                return new List<PositionalArgument>(query);
            }
        }

        /// Returns a list of the keyword arguments
        public List<KeywordArgument> KeywordArguments {
            get {
                var query = from arg in Arguments.Values
                            where arg is KeywordArgument
                            select arg as KeywordArgument;
                return new List<KeywordArgument>(query);
            }
        }

        /// Returns a list of the flag arguments
        public List<FlagArgument> FlagArguments {
            get {
                var query = from arg in Arguments.Values
                            where arg is FlagArgument
                            select arg as FlagArgument;
                return new List<FlagArgument>(query);
            }
        }

        /// Returns an enumerator for ValueArguments
        public IEnumerable<ValueArgument> ValueArguments {
            get {
                var query = from arg in Arguments.Values
                            where arg is ValueArgument
                            select arg as ValueArgument;
                return query;
            }
        }

        /// Access an argument by key, or alternatively by index for positional
        /// arguments (only).
        public Argument this[object key] {
            get {
                if(key is string) {
                    var sKey = key as string;
                    return Arguments.ContainsKey(sKey.ToLower()) ? Arguments[sKey.ToLower()] : null;
                }
                else if(key is int) {
                    var iKey = (int)key;
                    return iKey < PositionalArgumentOrder.Count ? Arguments[PositionalArgumentOrder[iKey]] : null;
                }
                else {
                    throw new ArgumentException("Arguments can be accessed by name or index only");
                }
            }
        }


        /// Adds an Argument definition to the list of arguments this command-line
        /// supports.
        public void AddArgument(Argument arg)
        {
            Arguments.Add(arg.Key.ToLower(), arg);
            if(arg is PositionalArgument) {
                PositionalArgumentOrder.Add(arg.Key.ToLower());
            }
        }


        /// Displays a usgae message, based on the allowed arguments and purpose
        /// represented by this class.
        public void DisplayUsage(string arg0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Purpose:");
            Console.Error.WriteLine("    {0}", string.Format(Purpose, Path.GetFileNameWithoutExtension(arg0)).Trim());

            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage:");
            Console.Error.Write("    {0}", arg0);
            foreach(var arg in PositionalArguments) {
                Console.Error.Write(arg.IsRequired ? " <{0}>" : " [<{0}>]", arg.Key);
            }
            if(KeywordArguments.Count > 0) {
                Console.Error.Write(" [<Key>:<Value> ...]");
            }
            if(FlagArguments.Count > 0) {
                Console.Error.Write(" [--<Flag> ...]");
            }
            Console.Error.WriteLine();

            if(PositionalArguments.Count > 0) {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Positional Arguments:");
                PositionalArguments.ForEach(x => OutputArg(x));
            }

            if(KeywordArguments.Count > 0) {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Keyword Arguments:");
                KeywordArguments.ForEach(x => OutputArg(x));
            }

            if(FlagArguments.Count > 0) {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Flag Arguments:");
                foreach(var arg in FlagArguments) {
                    Console.Error.WriteLine("    --{0,-14}  {1}", arg.Key, arg.Description);
                }
            }

        }

        // Outputs a single argument definition
        protected void OutputArg(ValueArgument arg) {
            Console.Error.Write("    {0,-16}  {1}", arg.Key, arg.Description);
            if(arg.DefaultValue != null) {
                Console.Error.Write(" (Default: {0})");
            }
            else if(arg.IsRequired) {
                Console.Error.Write(" (Required)");
            }
            Console.Error.WriteLine();
        }
    }


    /// Main class for interacting via the command-line. Handles the definition
    /// and parsing of command-line arguments, and the display of usage and help
    // messages.
    public class Interface
    {

        Definition Definition;


        /// Constructor; requires a purpose for the program whose args we are
        /// parsing.
        public Interface(string purpose)
        {
            Definition = new Definition { Purpose = purpose };
        }


        /// Convenience method for defining a new positional argument.
        public PositionalArgument AddPositionalArgument(string key, string desc)
        {
            var arg = new PositionalArgument { Key = key, Description = desc };
            Definition.AddArgument(arg);
            return arg;
        }

        /// Convenience method for defining a new keywork argument.
        public KeywordArgument AddKeywordArgument(string key, string desc)
        {
            var arg = new KeywordArgument { Key = key, Description = desc };
            Definition.AddArgument(arg);
            return arg;
        }

        /// Convenience method for defining a new keywork argument.
        public FlagArgument AddFlagArgument(string key, string desc)
        {
            var arg = new FlagArgument { Key = key, Description = desc };
            Definition.AddArgument(arg);
            return arg;
        }


        /// Parses the supplied set of arg strings using the list of Argument
        // definitions maintained by this command-line.
        public Dictionary<string, object> Parse(string[] args)
        {
            return new Parser(Definition).Parse(new List<string>(args));
        }

    }


    /// Exception thrown when there is an error parsing the command-line
    class ParseException : Exception
    {
        public ParseException(string msg)
            : base(msg)
        { }
    }


    /// <summary>
    /// Parses a command-line, using the argument definitions currently defined.
    /// </summary>
    class Parser
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected Definition Definition;

        protected List<string> PositionalValues;
        protected Dictionary<string, string> KeywordValues;
        protected List<string> FlagValues;
        protected bool ShowUsage = false;


        public Parser(Definition argDefs) {
            Definition = argDefs;
        }


        public Dictionary<string, object> Parse(List<string> args)
        {
            var result = new Dictionary<string, object>();

            // Classify the command-line entries passed to the program
            ClassifyArguments(args);

            // Process flag args
            foreach(var key in FlagValues) {
                var arg = Definition[key] as FlagArgument;
                if(arg != null) {
                    ProcessArgumentValue(result, arg, true);
                }
                else {
                    _log.WarnFormat("Unknown flag argument '--{0}' has been ignored", key);
                }
            }

            // Process positional args
            for(var i = 0; i < PositionalValues.Count; ++i) {
                var val = PositionalValues[i];
                var arg = Definition[i] as PositionalArgument;
                if(arg != null) {
                    ProcessArgumentValue(result, arg, val);
                }
                else {
                    _log.WarnFormat("Unknown positional argument '{0}' has been ignored", val);
                }

            }

            // Process keyword args
            foreach(var kv in KeywordValues) {
                var arg = Definition[kv.Key] as KeywordArgument;
                if(arg != null) {
                    ProcessArgumentValue(result, arg, kv.Value);
                }
                else {
                    _log.WarnFormat("Unknown keyword argument '{0}' has been ignored", kv.Key);
                }
            }

            // Check for missing arguments, set default values
            var missingArgs = 0;
            foreach(var arg in Definition.ValueArguments) {
                if(!result.ContainsKey(arg.Key)) {
                    if(arg.DefaultValue != null) {
                        _log.DebugFormat("Setting argument '{0}' to default value '{1}'", arg.Key, arg.DefaultValue);
                        result.Add(arg.Key, arg.DefaultValue);
                    }
                    else if(arg.IsRequired) {
                        _log.ErrorFormat("No value was specified for required argument '{0}'", arg.Key);
                        missingArgs++;
                    }
                }
            }
            if(missingArgs > 0) {
                throw new ParseException(string.Format("No value was specified for {0} required argument{1}",
                                         missingArgs, missingArgs > 1 ? "s" : ""));
            }

            if(ShowUsage) {
                Definition.DisplayUsage(args[0]);
            }

            return result;
        }


        /// Determine the kind of each argument value that has been supplied on
        /// the command line.
        protected void ClassifyArguments(List<string> args)
        {
            string key = null;
            int pos;

            PositionalValues = new List<string>();
            KeywordValues = new Dictionary<string, string>();
            FlagValues = new List<string>();

            foreach(var arg in args.Skip(1)) {
                if(arg == "/?" || arg.ToLower() == "--help") {
                    ShowUsage = true;
                    if(key != null) {
                        KeywordValues.Add(key, null);
                        key = null;
                    }
                }
                else if(key != null) {
                    // This argument follows a keyword key of the form -key
                    if(Regex.IsMatch(arg, @"(^[/-])|(^\w\w+:)")) {
                        // This arg is not a keyword value
                        KeywordValues.Add(key, null);
                    }
                    else {
                        KeywordValues.Add(key, arg);
                    }
                    key = null;
                }
                else {
                    if(arg.StartsWith("--")) {
                        FlagValues.Add(arg.Substring(2));
                    }
                    else if(arg.StartsWith("/")) {
                        FlagValues.Add(arg.Substring(1));
                    }
                    else if((pos = arg.IndexOf(":")) > 1) {
                        KeywordValues.Add(arg.Substring(0, pos), arg.Substring(pos + 1));
                    }
                    else if(arg.StartsWith("-")) {
                        key = arg.Substring(1);
                    }
                    else { // Argument is positional
                        PositionalValues.Add(arg);
                    }
                }
            }
            if(key != null) {
                KeywordValues.Add(key, null);
            }
        }


        /// Processes a single argument value, ensuring it passes validation,
        /// calling any OnParseHandlers etc.
        protected void ProcessArgumentValue(Dictionary<string, object> result, Argument arg, object val) {
            // Validate value, if argument specifies validation
            var valArg = arg as ValueArgument;
            var sVal = val as string;
            if(valArg != null && valArg.Validation != null && sVal != null) {
                if(!valArg.Validation.IsMatch(sVal)) {
                    throw new ParseException(string.Format("The value '{0}' for argument {1} is not valid", sVal, arg.Key));
                }
            }

            result.Add(arg.Key, val);
            if(arg.OnParse != null) {
                arg.OnParse(arg.Key, val as string);
            }
        }
    }

}
