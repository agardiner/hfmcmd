using System;
using System.Collections.Generic;
using System.Linq;


namespace CommandLine
{

    /// <summary>
    /// Class representing a set of Argument definitions.
    /// </summary>
    public class Definition
    {
        /// Collection of Argument objects, defining the permitted/expected
        /// arguments this program takes.
        internal Dictionary<string, Argument> Arguments =
                new Dictionary<string, Argument>(StringComparer.OrdinalIgnoreCase);
        /// List identifying the insertion order of positional arguments.
        private List<string> _positionalArgumentOrder = new List<string>();


        // Properties

        /// Details on how to get help about the program whose command-line
        /// arguments we are defining.
        public string HelpInstructions { get; set; }
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

    }

}
