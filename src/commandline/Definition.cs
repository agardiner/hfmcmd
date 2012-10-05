using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Class representing a set of Argument definitions.
    /// </summary>
    public class Definition
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Collection of Argument objects, defining the permitted/expected
        /// arguments this program takes.
        internal Dictionary<string, Argument> Arguments = new Dictionary<string, Argument>();
        /// List identifying the insertion order of positional arguments.
        private List<string> _positionalArgumentOrder = new List<string>();
        /// Reference to IArgumentMapper used to convert arguments to required types
        private IArgumentMapper _argumentMapper;


        // Properties

        /// Purpose of the program whose command-line arguments we are parsing.
        public string Purpose { get; set; }

        /// An IArgumentMapper implementation to handle conversion of parsed
        /// string values to Argument.Type instances.
        public IArgumentMapper ArgumentMapper
        {
            get {
                return _argumentMapper;
            }
            set {
                _argumentMapper = value;
                foreach(var arg in ValueArguments) {
                    ValidateArgType(arg);
                }
            }
        }


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
                    return iKey < _positionalArgumentOrder.Count ? Arguments[_positionalArgumentOrder[iKey]] : null;
                }
                else {
                    throw new ArgumentException("Arguments can be accessed by name or index only");
                }
            }
        }


        /// Validates argument type conversion is possible.
        private void ValidateArgType(Argument arg)
        {
            if(arg is ValueArgument && arg.Type != typeof(string)) {
                if(_argumentMapper != null) {
                    if(!_argumentMapper.CanConvert(arg.Type)) {
                        throw new ArgumentException(string.Format(
                                "ArgumentMapper cannot handle conversion of strings to {0}",
                                arg.Type));
                    }
                }
                else {
                    throw new ArgumentException(string.Format(
                            "No ArgumentMapper is registered, and argument {0} specifies a Type of {1}",
                            arg.Key, arg.Type));
                }
            }
        }


        /// <summary>
        /// Adds an Argument definition to the list of arguments this command-line
        /// supports.
        /// </summary>
        public Argument AddArgument(Argument arg)
        {
            Arguments.Add(arg.Key.ToLower(), arg);
            if(arg is PositionalArgument) {
                _positionalArgumentOrder.Add(arg.Key.ToLower());
            }
            ValidateArgType(arg);
            return arg;
        }


        /// <summary>
        /// Displays a usgae message, based on the allowed arguments and purpose
        /// represented by this class.
        /// </summary>
        public void DisplayUsage(string arg0, TextWriter console)
        {
            console.WriteLine();
            console.WriteLine("Purpose:");
            console.WriteLine("    {0}", string.Format(Purpose,
                        Path.GetFileNameWithoutExtension(arg0)).Trim());

            console.WriteLine();
            console.WriteLine("Usage:");
            console.Write("    {0}", arg0);
            foreach(var arg in PositionalArguments) {
                console.Write(arg.IsRequired ? " <{0}>" : " [<{0}>]", arg.Key);
            }
            if(KeywordArguments.Count > 0) {
                console.Write(" [<Key>:<Value> ...]");
            }
            if(FlagArguments.Count > 0) {
                console.Write(" [--<Flag> ...]");
            }
            console.WriteLine();

            if(PositionalArguments.Count > 0) {
                console.WriteLine();
                console.WriteLine("Positional Arguments:");
                PositionalArguments.ForEach(x => OutputArg(x, console));
            }

            if(KeywordArguments.Count > 0) {
                console.WriteLine();
                console.WriteLine("Keyword Arguments:");
                KeywordArguments.ForEach(x => OutputArg(x, console));
            }

            if(FlagArguments.Count > 0) {
                console.WriteLine();
                console.WriteLine("Flag Arguments:");
                FlagArguments.ForEach(x => OutputArg(x, console));
            }

        }

        /// Outputs a single argument definition
        protected void OutputArg(Argument arg, TextWriter console) {
            if(arg is FlagArgument) {
                console.Write("    --{0,-14}  {1}", arg.Key, arg.Description);
            }
            else {
                console.Write("    {0,-16}  {1}", arg.Key, arg.Description);
            }
            var valArg = arg as ValueArgument;
            if(valArg != null) {
                if(valArg.DefaultValue != null) {
                    console.Write(" (Default: {0})", valArg.DefaultValue);
                }
                else if(valArg.IsRequired) {
                    console.Write(" (Required)");
                }
            }
            console.WriteLine();
        }

    }

}
