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
        internal Dictionary<string, Argument> Arguments =
                new Dictionary<string, Argument>(StringComparer.OrdinalIgnoreCase);
        /// List identifying the insertion order of positional arguments.
        private List<string> _positionalArgumentOrder = new List<string>();


        // Properties

        /// Purpose of the program whose command-line arguments we are parsing.
        public string Purpose { get; set; }
        /// Flag governing whether unknown keyword args should be included in
        /// the parse results
        public bool IncludeUnrecognisedKeywordArgs = false;
        /// Flag governing whether unknown flag args should be included in the
        /// parse results
        public bool IncludeUnrecognisedFlagArgs = false;


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

        /// Access an argument by key, alias, or alternatively by index for positional
        /// arguments (only).
        public Argument this[object key] {
            get {
                if(key is string) {
                    var sKey = key as string;
                    if(Arguments.ContainsKey(sKey)) {
                        return Arguments[sKey];
                    }
                    else {
                        return Arguments.Values.FirstOrDefault(a =>
                                string.Compare(sKey, a.Alias, StringComparison.OrdinalIgnoreCase) == 0);
                    }
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


        /// <summary>
        /// Adds an Argument definition to the list of arguments this command-line
        /// supports.
        /// </summary>
        public Argument AddArgument(Argument arg)
        {
            Arguments.Add(arg.Key, arg);
            if(arg is PositionalArgument) {
                _positionalArgumentOrder.Add(arg.Key);
            }
            return arg;
        }


        /// <summary>
        /// Displays a usgae message, based on the allowed arguments and purpose
        /// represented by this class.
        /// </summary>
        public void DisplayUsage(string arg0, TextWriter console, Dictionary<string, object> args)
        {
            string exe = Path.GetFileNameWithoutExtension(arg0);

            console.WriteLine();
            console.WriteLine("Purpose:");
            console.WriteLine("    {0}", string.Format(Purpose, exe).Trim());

            console.WriteLine();
            console.WriteLine("Usage:");
            console.Write("    {0}", exe);

            var posArgs = PositionalArguments.ToList();
            var i = 0;
            foreach(var arg in PositionalArguments) {
                if(arg.IsCommand && (args.ContainsKey(arg.Key) ||
                   (arg.Alias != null && args.ContainsKey(arg.Alias)))) {
                    console.Write(arg.IsRequired ? " {0}" : " [{0}]",
                            args.ContainsKey(arg.Key) ? args[arg.Key] : args[arg.Alias]);
                    posArgs.RemoveAt(i);
                }
                else {
                    console.Write(arg.IsRequired ? " <{0}>" : " [<{0}>]", arg.Key);
                }
                i++;
            }
            if(KeywordArguments.Count > 0) {
                console.Write(" [<Key>:<Value> ...]");
            }
            if(FlagArguments.Count > 0) {
                console.Write(" [--<Flag> ...]");
            }
            console.WriteLine();

            if(posArgs.Count > 0) {
                console.WriteLine();
                console.WriteLine("Positional Arguments:");
                posArgs.ForEach(x => OutputArg(x, console));
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
