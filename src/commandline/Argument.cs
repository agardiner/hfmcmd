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
        public delegate void OnParseHandler(Argument arg, string value);

        /// The key that will identify this argument in the parse result.
        public string Key { get; set; }
        /// An alternate key that can be used to reference this argument.
        /// Usually this is used for arguments that can be singular or plural.
        public string Alias { get; set; }
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
        public delegate bool ValidateHandler(Argument arg, string val, out string errorMsg);
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
        /// True if this positional argument represents a command; this is
        /// relevant when e.g. the first positional argument is used to
        /// determine what other arguments to expect.
        public bool IsCommand { get; set; }

        /// Positional arguments default to required.
        public PositionalArgument()
        {
            IsRequired = true;
            IsCommand = false;
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
