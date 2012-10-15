using System;


namespace CommandLine
{

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

}
