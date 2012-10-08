using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Exception thrown when there is an error parsing the command-line
    /// </summary>
    public class ParseException : Exception
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

        /// Set to true when the parser encounters a help flag (/? or --help)
        public bool ShowUsage;
        /// The parse exception that was thrown if an error was encountered
        /// during parsing
        public ParseException ParseException;


        /// Constructor
        public Parser(Definition argDefs) {
            Definition = argDefs;
        }


        /// <summary>
        /// Parse the supplied list of arguments, using the argument definitions
        /// given at construction.
        /// </summary>
        /// <returns>Null if we displayed usage instructions, otherwise a
        /// Dictionary of keys matching each argument found or defaulted,
        /// whose values are the string value of the argument for non-flag
        /// args, or a boolean value for flag args.
        /// </returns>
        public Dictionary<string, object> Parse(List<string> args)
        {
            Dictionary<string, object> result = null;

            ShowUsage = false;
            ParseException = null;

            _log.Fine("Parsing command-line arguments...");

            // Classify the command-line entries passed to the program
            ClassifyArguments(args);

            try {
                result = ProcessArguments();
            }
            catch(ParseException ex) {
                ParseException = ex;
            }

            return result;
        }


        /// Determine the kind of each argument value that has been supplied on
        /// the command line, based on whether it has a flag prefix, keyword etc.
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


        /// Using the now classified argument values, match them to corresponding
        /// argument definitions, then identify missing arguments and set any
        /// default values.
        protected Dictionary<string, object> ProcessArguments() {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string set = null;

            // Process flag args
            foreach(var key in FlagValues) {
                var arg = Definition[key];
                if(arg != null) {
                    set = ProcessArgumentValue(result, arg, true, set);
                }
                else if(Definition.IncludeUnrecognisedFlagArgs) {
                    _log.TraceFormat("Unrecognised flag argument '--{0}' has been set", key);
                    result[key] = true;
                }
                else if(!ShowUsage) {
                    _log.WarnFormat("Unknown flag argument '--{0}' has been ignored", key);
                }
            }

            // Process positional args
            for(var i = 0; i < PositionalValues.Count; ++i) {
                var val = PositionalValues[i];
                var arg = Definition[i];
                if(arg != null) {
                    set = ProcessArgumentValue(result, arg, val, set);
                }
                else if(!ShowUsage) {
                    _log.WarnFormat("Unknown positional argument '{0}' has been ignored", val);
                }

            }

            // Process keyword args
            foreach(var kv in KeywordValues) {
                var arg = Definition[kv.Key];
                if(arg != null) {
                    set = ProcessArgumentValue(result, arg, kv.Value, set);
                }
                else if(Definition.IncludeUnrecognisedKeywordArgs) {
                    _log.TraceFormat("Unrecognised keyword argument '{0}' has been set", kv.Key);
                    result[kv.Key] = kv.Value;
                }
                else if(!ShowUsage) {
                    _log.WarnFormat("Unknown keyword argument '{0}' has been ignored", kv.Key);
                }
            }

            // Check for missing arguments, set default values
            var missingArgs = 0;
            foreach(var arg in Definition.ValueArguments) {
                if(!result.ContainsKey(arg.Key) && (set == null || arg.Set == null || arg.Set == set)) {
                    _log.DebugFormat("No value was specified for argument {0}", arg.Key);
                    if(arg.DefaultValue != null) {
                        _log.TraceFormat("Setting argument {0} to default value '{1}'", arg.Key, arg.DefaultValue);
                        if(Definition.ArgumentMapper != null && arg.Type != typeof(string)) {
                            // Convert argument value to required type
                            result.Add(arg.Key, Definition.ArgumentMapper.
                                       ConvertArgument(arg, arg.DefaultValue, result));
                        }
                        else {
                            result.Add(arg.Key, arg.DefaultValue);
                        }
                    }
                    else if(arg.IsRequired && !ShowUsage) {
                        _log.ErrorFormat("No value was specified for required argument '{0}'", arg.Key);
                        missingArgs++;
                    }
                }
            }
            if(missingArgs > 0 && !ShowUsage) {
                throw new ParseException(string.Format("No value was specified for {0} required argument{1}",
                                         missingArgs, missingArgs > 1 ? "s" : ""));
            }

            return result;
        }


        /// Processes a single argument value, ensuring it passes validation,
        /// calling any OnParse handlers etc.
        protected string ProcessArgumentValue(Dictionary<string, object> result, Argument arg, object val, string set) {
            // Validate value, if argument specifies validation
            string errorMsg;
            var valArg = arg as ValueArgument;
            var sVal = val as string;

            if(set != null) {
                // Argument must be a member of the same set, or a member of no set
                _log.DebugFormat("Checking argument {0} is valid for argument set {1}", arg.Key, set);
                if(arg.Set != null && arg.Set != set) {
                   throw new ParseException(string.Format("Cannot mix arguments from sets {0} and {1}",
                                arg.Set, set));
                }
            }
            else if(arg.Set != null) {
                set = arg.Set;
                _log.DebugFormat("Argument set is {0}", set);
            }

            if(valArg != null) {
                if(valArg.Validate != null && sVal != null) {
                    _log.DebugFormat("Validating argument {0}", valArg.Key);
                    foreach(ValueArgument.ValidateHandler validate in valArg.Validate.GetInvocationList()) {
                        if(!validate(sVal, out errorMsg)) {
                            throw new ParseException(string.Format("The value '{0}' for argument {1} is not valid. {2}.",
                                                     sVal, arg.Key, errorMsg));
                        }
                    }
                    _log.TraceFormat("Argument {0} validated", valArg.Key);
                }
                _log.TraceFormat("Setting {0} to '{1}'", arg.Key, valArg.IsSensitive ? "******" : val);

                if(Definition.ArgumentMapper != null && arg.Type != typeof(string)) {
                    // Convert argument value to required type
                    val = Definition.ArgumentMapper.ConvertArgument(arg, sVal, result);
                }
            }
            else {
                _log.TraceFormat("Setting flag {0} to true", arg.Key);
            }

            result.Add(arg.Key, val);

            // Call OnParse event, if handler specified
            if(arg.OnParse != null) {
                arg.OnParse(arg.Key, val as string);
            }
            return set;
        }

    }

}
