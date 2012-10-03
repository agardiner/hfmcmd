using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Interface for an argument validator. Defines a single method IsValid
    /// that implementations must implement.
    /// </summary>
    public interface ArgumentValidator
    {
        /// <summary>
        /// Primary method for argument validation; returns true if the argument
        /// is valid.
        /// </summary>
        /// <param name="value">The argument string value to be validated. Will
        /// never be null.</param>
        /// <param name="errorMsg">An optional out parameter for returning
        /// additional details on the reason for failure if an argument fails
        /// validation.</param>
        bool IsValid(string value, out string errorMsg);
    }



    /// <summary>
    /// Base class for all types of command-line arguments.
    /// Each argument must have a key that will be used to access the value in
    /// the returned Dictionary, and a description that is shown when help is
    /// displayed.
    /// </summary>
    public abstract class Argument
    {
        /// Delegate definition for callback after parsing this argument
        public delegate void OnParseHandler(string key, string value);

        /// The key that will identify this argument in the parse result.
        public string Key { get; set; }
        /// The argument description
        public string Description { get; set; }
        /// The type the argument should be converted to after parsing. Default
        /// behaviour is to leave argument values as strings. To change this,
        /// argument definitions should specify the desired Type values should
        /// be converted to, and register an ArgumentMapper instance that can
        /// handle the conversion from strings to objects of this Type.
        public Type Type { get; set; }
        /// The name of the argument set this argument belongs to (if any)
        public string Set { get; set; }
        /// A callback to be called when the argument has been parsed successfully
        public OnParseHandler OnParse { get; set; }
    }

    /// <summary>
    /// ValueArguments are arguments that take a value. This is the parent class
    /// for positional and keyword arguments.
    /// </summary>
    public abstract class ValueArgument : Argument
    {
        /// Whether the argument is required or optional
        public bool IsRequired { get; set; }
        /// The default value for the argument
        public string DefaultValue { get; set; }
        /// Define the signature for the Validate event
        public delegate bool ValidateHandler(string val, out string errorMsg);
        /// An optional validation callback for validating argument values
        public ValidateHandler Validate;
        /// A flag indicating whether the argument represents a sensitive value
        /// such as a password
        public bool IsSensitive { get; set; }

        public void AddValidator(ArgumentValidator validator)
        {
            Validate += new ValidateHandler(validator.IsValid);
        }
    }

    /// <summary>
    /// Positional arguments are those where a value is specified without its
    /// key; the order of argument values identify the positional argument.
    /// </summary>
    public class PositionalArgument : ValueArgument
    {
        /// Positional arguments default to required.
        public PositionalArgument()
        {
            IsRequired = true;
        }
    }

    /// <summary>
    /// A keyword argument can appear in any position, since its key tells us
    /// which argument it corresponds to. The key may be specified as a prefix
    /// to the value, i.e. key:value, or as a separate argument preceding the
    /// value.
    /// </summary>
    public class KeywordArgument : ValueArgument {}

    /// <summary>
    /// Flag arguments are booleans that are set if the flag is encountered.
    /// A flag argument is identified by a -- or / prefix.
    /// </summary>
    public class FlagArgument : Argument {}



    /// <summary>
    /// Validator implementation using a regular expression.
    /// </summary>
    public class RegexValidator : ArgumentValidator
    {
        public Regex Expression { get; set; }

        public RegexValidator()
        {
        }

        public RegexValidator(string re)
        {
            Expression = new Regex(re);
        }

        public RegexValidator(Regex re)
        {
            Expression = re;
        }

        public bool IsValid(string value, out string errorMsg) {
            errorMsg = null;
            if(Expression == null) {
                return true;
            }
            else {
                errorMsg = String.Format("Value must satisfy the regular expression: /{0}/", Expression);
                return Expression.IsMatch(value);
            }
        }
    }


    /// <summary>
    /// ArgumentValidator implementation using a list of values.
    /// Supports validations of both single and multiple comma-separated argument
    /// values via the PermitMultipleValues property. Argument validation is not
    /// case-sensitive, unless the CaseSensitive property is set to true.
    /// </summary>
    public class ListValidator : ArgumentValidator
    {
        public List<string> Values { get; set; }
        public bool CaseSensitive { get; set; }
        public bool PermitMultipleValues { get; set; }

        public ListValidator()
        {
        }

        public ListValidator(List<string> values)
        {
            Values = values;
        }

        public ListValidator(params string[] values)
        {
            Values = new List<string>(values);
        }

        public bool IsValid(string value, out string errorMsg) {
            var ok = true;
            errorMsg = null;
            if(Values != null) {
                var values = PermitMultipleValues ? value.Split(',') : new string[] { value };
                foreach(var val in values) {
                    ok = ok && (CaseSensitive ? Values.Contains(val) :
                            Values.Contains(val, StringComparer.OrdinalIgnoreCase));
                }
            }
            if(!ok) {
                errorMsg = String.Format("Value must be {0} of: {1}{2}",
                                         PermitMultipleValues ? "any" : "one",
                                         String.Join(", ", Values.ToArray()),
                                         PermitMultipleValues ? " (Use a comma to separate multiple values)" : "");
            }
            return ok;
        }
    }


    /// <summary>
    /// ArgumentValidator implementation using a range.
    /// </summary>
    public class RangeValidator : ArgumentValidator
    {
        int? Min { get; set; }
        int? Max { get; set; }

        public RangeValidator()
        {
        }

        public RangeValidator(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public bool IsValid(string value, out string errorMsg) {
            int iVal = int.Parse(value);
            errorMsg = null;
            if(Min != null && Max != null) {
                errorMsg = String.Format("Value must be in the range {0} to {1} inclusive", Min, Max);
            }
            else if(Min != null) {
                errorMsg = String.Format("Value must be greater than or equal to {0}", Min);
            }
            else if(Max != null) {
                errorMsg = String.Format("Value must be less than or equal to {0}", Max);
            }
            return (Min == null || iVal >= Min) && (Max == null || iVal <= Max);
        }
    }



    /// <summary>
    /// Command-line argument parsing can optionally convert values read from
    /// the command-line into objects of different types. To enable this
    /// functionality, a command-line definition can register an implementation
    /// of this interface. The implementing object will then be called upon to
    /// perform conversions of any non-string parameter types (i.e. those whose
    /// Type property is a non-string Type).
    /// </summary>
    public interface IArgumentMapper
    {
        bool CanConvert(Type type);
        object ConvertArgument(Argument arg, string val, Dictionary<string, object> argVals);
    }



    /// <summary>
    /// Provides a general purpose and extensible IArgumentMapper implementation.
    /// String-to-object type conversions can be registered in an instance of
    /// this class via lambda functions. Additionally, default mappings for
    /// int, bool, and string[] are provided.
    /// </summary>
    public class PluggableArgumentMapper : IArgumentMapper
    {
        private Dictionary<Type, Func<string, Type, Dictionary<string, object>, object>> _maps;

        public Func<string, Type, Dictionary<string, object>, object> this[Type type] {
            get { return _maps[type]; }
            set { _maps[type] = value; }
        }


        /// Default constructor, which registers default conversions
        public PluggableArgumentMapper() : this(true) { }


        /// <summary>
        /// Construct a new instance, and registers default conversions if
        /// includeDefaults is true.
        /// </summary>
        public PluggableArgumentMapper(bool includeDefaults) {
            _maps = new Dictionary<Type, Func<string, Type, Dictionary<string, object>, object>>();
            if(includeDefaults) {
                this[typeof(string)] = (val, type, args) => val;
                this[typeof(int)] = (val, type, args) => int.Parse(val);
                this[typeof(double)] = (val, type, args) => double.Parse(val);
                this[typeof(bool)] = (val, type, args) =>
                    new Regex("^t(rue)?|y(es)?$", RegexOptions.IgnoreCase).IsMatch(val);
                this[typeof(DateTime)] = (val, type, args) => DateTime.Parse(val);
            }
        }


        /// <summary>
        /// Remove the map expression for type.
        /// </summary>
        public void Remove(Type type)
        {
            _maps.Remove(type);
        }


        /// <summary>
        /// Returns true if type can be converted, false if it cannot.
        /// </summary>
        public bool CanConvert(Type type)
        {
            if(type.IsArray) {
                return type.GetElementType().IsEnum || _maps.ContainsKey(type.GetElementType());
            }
            else if(type.IsEnum) {
                return true;
            }
            else {
                return _maps.ContainsKey(type);
            }
        }


        /// <summary>
        /// Returns the result of converting the supplied argument.
        /// </summary>
        public object ConvertArgument(Argument arg, string value, Dictionary<string, object> argVals)
        {
            if(CanConvert(arg.Type)) {
                if(_maps.ContainsKey(arg.Type)) {
                    return _maps[arg.Type](value, arg.Type, argVals);
                }
                else if(arg.Type.IsEnum) {
                    return ConvertEnum(value, arg.Type);
                }
                else if(arg.Type.IsArray) {
                    return ConvertArray(value, arg.Type.GetElementType(), argVals);
                }
            }
            throw new ArgumentException(
                    string.Format("No conversion is registered for type {0}", arg.Type));
        }


        /// Converts a string to an enum
        protected object ConvertEnum(string val, Type type)
        {
            // Try to parse value as provided
            try {
                return Enum.Parse(type, val);
            }
            catch (ArgumentException)
            { }

            // OK, so see if exactly one enum value ends with the supplied value
            try {
                var enumVal = Enum.GetNames(type).Single(
                        name => name.EndsWith(val, StringComparison.OrdinalIgnoreCase));
                return Enum.Parse(type, enumVal);
            }
            catch (Exception ex) {
                throw new ArgumentException(string.Format("Invalid value '{0}' specified for {1}. Valid values are: {2}",
                            val, type, string.Join(", ", Enum.GetNames(type))), ex);
            }
        }


        /// Converts a string to an array of objects
        protected object ConvertArray(string val, Type type, Dictionary<string, object> argVals)
        {
            string[] vals = val.Split(',');
            var obj = Array.CreateInstance(type, vals.Length); //.Cast<object>().ToArray();
            for(int i = 0; i < vals.Length; ++i) {
                obj.SetValue(ConvertArrayElement(vals[i], type, argVals), i);
            }
            return obj;
        }


        /// Converts a single element of an array
        protected object ConvertArrayElement(string val, Type type, Dictionary<string, object> argVals)
        {
            if(_maps.ContainsKey(type)) {
                return _maps[type](val, type, argVals);
            }
            else if(type.IsEnum) {
                return ConvertEnum(val, type);
            }
            else {
                throw new ArgumentException(
                        string.Format("No conversion is registered for type {0}", type));
            }
        }

    }



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

        /// Outputs a single argument definition
        protected void OutputArg(ValueArgument arg) {
            Console.Error.Write("    {0,-16}  {1}", arg.Key, arg.Description);
            if(arg.DefaultValue != null) {
                Console.Error.Write(" (Default: {0})", arg.DefaultValue);
            }
            else if(arg.IsRequired) {
                Console.Error.Write(" (Required)");
            }
            Console.Error.WriteLine();
        }
    }



    /// <summary>
    /// Main class for interacting via the command-line. Handles the definition
    /// and parsing of command-line arguments, and the display of usage and help
    /// messages.
    /// </summary>
    public class UI
    {
        /// Flag indicating whether console output is redirected
        private bool _isRedirected = false;

        /// The set of possible arguments to be recognised.
        public Definition Definition;

        /// Returns the console width, or -1 if the console is redirected
        public int ConsoleWidth {
            get {
                if(_isRedirected) { return -1; }
                try {
                    return System.Console.WindowWidth;
                }
                catch(IOException) {
                    _isRedirected = true;
                    return -1;
                }
            }
        }


        /// <summary>
        /// Constructor; requires a purpose for the program whose args we are
        /// parsing.
        /// </summary>
        public UI(string purpose) : this(purpose, null) {}


        /// <summary>
        /// Constructor; requires a purpose for the program whose args we are
        /// parsing.
        /// </summary>
        public UI(string purpose, IArgumentMapper argMap)
        {
            Definition = new Definition { Purpose = purpose, ArgumentMapper = argMap };
        }


        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc)
        {
            return AddPositionalArgument(key, desc, typeof(string), null);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Type type)
        {
            return AddPositionalArgument(key, desc, type, null);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Argument.OnParseHandler onParse)
        {
            return AddPositionalArgument(key, desc, typeof(string), onParse);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Type type,
                Argument.OnParseHandler onParse)
        {
            var arg = new PositionalArgument { Key = key, Description = desc, Type = type };
            arg.OnParse += onParse;
            return (PositionalArgument)Definition.AddArgument(arg);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc)
        {
            return AddKeywordArgument(key, desc, typeof(string), null);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Type type)
        {
            return AddKeywordArgument(key, desc, type, null);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Argument.OnParseHandler onParse)
        {
            return AddKeywordArgument(key, desc, typeof(string), onParse);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Type type,
                Argument.OnParseHandler onParse)
        {
            var arg = new KeywordArgument { Key = key, Description = desc, Type = type };
            arg.OnParse += onParse;
            return (KeywordArgument)Definition.AddArgument(arg);
        }

        /// <summary>
        /// Convenience method for defining a new flag argument.
        /// </summary>
        public FlagArgument AddFlagArgument(string key, string desc)
        {
            return AddFlagArgument(key, desc, null);
        }


        /// <summary>
        /// Convenience method for defining a new flag argument.
        /// </summary>
        public FlagArgument AddFlagArgument(string key, string desc,
                Argument.OnParseHandler onParse)
        {
            var arg = new FlagArgument { Key = key, Description = desc, Type = typeof(bool) };
            arg.OnParse += onParse;
            return (FlagArgument)Definition.AddArgument(arg);
        }


        /// <summary>
        /// Parses the supplied set of arg strings using the list of Argument
        // definitions maintained by this command-line UI instance.
        /// </summary>
        public Dictionary<string, object> Parse(string[] args)
        {
            return new Parser(Definition).Parse(new List<string>(args));
        }


        /// <summary>
        /// Writes a line of text to the console, ensuring lines the same width
        /// as the console don't output an unnecessary new-line.
        /// </summary>
        public void WriteLine(string line)
        {
            if(line.Length == ConsoleWidth) {
                System.Console.Out.Write(line);
            }
            else {
                System.Console.Out.WriteLine(line);
            }
        }


        /// <summary>
        /// Writes a line of text to the console, ensuring lines the same width
        /// as the console don't output an unnecessary new-line.
        /// </summary>
        public void WriteLine()
        {
            System.Console.Out.WriteLine();
        }


        /// <summary>
        /// Writes a partial line of text to the console, without moving to the
        /// next line.
        /// </summary>
        public void Write(string line)
        {
            System.Console.Out.Write(line);
        }


        /// <summary>
        /// Clears the current line of the console.
        /// </summary>
        public void ClearLine()
        {
            if(ConsoleWidth > -1 && System.Console.CursorLeft > 0) {
                var buf = new char[ConsoleWidth - 1];
                System.Console.CursorLeft = 0;
                System.Console.Write(buf);
                System.Console.CursorLeft = 0;
            }
        }


        /// <summary>
        /// Returns true if escape has been pressed.
        /// </summary>
        public bool EscPressed()
        {
            bool esc = false;

            if (System.Console.KeyAvailable) {
                var keyInfo = System.Console.ReadKey();
                if(keyInfo.Key == System.ConsoleKey.Escape) {
                    esc = true;
                }
            }
            return esc;
        }

    }



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
        protected bool ShowUsage = false;


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
            ParseException pe = null;

            _log.Fine("Parsing command-line arguments...");

            // Classify the command-line entries passed to the program
            ClassifyArguments(args);

            try {
                result = ProcessArguments();
            }
            catch(ParseException ex) {
                pe = ex;
            }

            if(ShowUsage) {
                Definition.DisplayUsage(args[0]);
                result = null;  // Don't act on what we parsed
            }
            else if(pe != null) {
                throw pe;
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
                else {
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
                else {
                    _log.WarnFormat("Unknown positional argument '{0}' has been ignored", val);
                }

            }

            // Process keyword args
            foreach(var kv in KeywordValues) {
                var arg = Definition[kv.Key];
                if(arg != null) {
                    set = ProcessArgumentValue(result, arg, kv.Value, set);
                }
                else {
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
                            result.Add(arg.Key, Definition.ArgumentMapper.ConvertArgument(arg, arg.DefaultValue, result));
                        }
                        else {
                            result.Add(arg.Key, arg.DefaultValue);
                        }
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
