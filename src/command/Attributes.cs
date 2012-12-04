using System;
using System.Reflection;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Base class of Command, Parameter, and Setting attributes.
    /// </summary>
    public abstract class VersionedAttribute : Attribute
    {
        /// Description of the command (mandatory)
        protected string _description;
        /// Holds the UDA(s) this item has been tagged with, as a | delimited
        /// string.
        protected string _uda;

        /// An alternate name (or alias) by which the item may also be
        /// referenced. Typically used for settings that may be singular or
        /// plural.
        public string Alias { get; set; }
        /// True if the setting has an alternate name
        public bool HasAlias { get { return Alias != null; } }
        /// Description of command or setting
        public string Description { get { return _description; } }
        /// Version in which the command was introduced
        public string Since { get; set; }
        /// Version in which the command was deprecated
        public string Deprecated { get; set; }
        /// Returns true if there is a Since or Deprecated attribute
        public bool IsVersioned { get { return Since != null || Deprecated != null; } }
        /// User-defined attribute; can be used to store additional information
        /// about the setting.
        public string Uda {
            get {
                return _uda;
            }
            set {
                if(_uda == null) {
                    _uda = value;
                }
                else {
                    _uda += "|" + value;
                }
            }
        }


        /// Constructor; forces description to be mandatory
        public VersionedAttribute(string desc)
        {
            _description = desc;
        }


        /// Checks if the setting is current for a specified version, based on
        /// the Since and/or Deprecated settings.
        public bool IsCurrent(Version version)
        {
            bool current = true;
            if(IsVersioned) {
                var from = Since != null ? new Version(Since) : null;
                var to = Deprecated != null ? new Version(Deprecated) : null;
                current = (from == null || version >= from) && (to == null || version < to);
            }
            return current;
        }


        /// <summary>
        /// Returns true if this setting is tagged with the specified user-
        /// defined attribute.
        /// </summary>
        public bool HasUda(string uda)
        {
            bool hasUda = false;
            if(_uda != null) {
                hasUda = _uda.Split('|').Contains(uda, StringComparer.OrdinalIgnoreCase);
            }
            return hasUda;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CommandAttribute : VersionedAttribute
    {
        /// Name of the command; this is normally the same as the method that is
        /// decorated with this attribute
        public string Name { get; set; }

        /// Constructor
        public CommandAttribute(string desc)
            : base(desc)
        { }
    }



    /// </summary>
    /// Define an attribute that can be used to tag a method, property, or
    /// constructor as a source of new instances of the class returned by
    /// the member. This information will be used by Context objects to
    /// determine how to obtain objects of the required type when invoking a
    /// Command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property,
     AllowMultiple = false)]
    public class FactoryAttribute : Attribute
    {
        /// Flag indicating whether this Factory is the primary means for
        /// obtaining these objects, or an alternate
        public bool Alternate = false;
        /// Flag indicating whether factory object can be persisted in context
        /// and reused for other command invocations
        public bool SingleUse = false;
    }



    /// <summary>
    /// Abstract base class for Parameter and Setting attributes.
    /// </summary>
    public abstract class ValueAttribute : VersionedAttribute
    {
        /// The default value (if any) for this setting
        protected object _defaultValue = null;
        /// A flag indicating whether the attribute has a default value, since
        /// checking for null is not sufficient (the default value may be null).
        protected bool _hasDefaultValue = false;

        /// Whether the parameter has a DefaultValue (since the default may be null)
        public bool HasDefaultValue { get { return _hasDefaultValue; } }
        /// Default value for parameter
        public object DefaultValue {
            get {
                return _defaultValue;
            }
            set {
                _defaultValue = value;
                _hasDefaultValue = true;
            }
        }
        /// Whether the setting is a sensitive value that should not be logged
        public bool   IsSensitive { get; set; }
        /// The constant part of the enum label that can be omitted
        public string EnumPrefix { get; set; }

        /// Constructor; forces description to be mandatory
        public ValueAttribute(string desc)
            : base(desc)
        { }
    }



    /// <summary>
    /// Define an attribute which will be used to specify command parameter
    /// information, such as default values, sensitive flags, enum prefixes,
    /// etc.
    /// </summary>
    /// <remarks>
    /// DefaultValues are captured through attributes instead of default values
    /// on the actual method parameters, since a) default parameter values are
    /// only available from v4 of .Net, and b) they have restrictions on where
    /// they can appear (only at the end of the list of parameters).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ParameterAttribute : ValueAttribute
    {
        public ParameterAttribute(string desc)
            : base(desc)
        { }
    }



    /// <summary>
    /// Used to define the possible settings that can appear in an
    /// ISettingsCollection subclass.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SettingAttribute : ValueAttribute, ISetting
    {
        /// Name of the setting
        public string Name { get; set; }
        /// The Type of value which the setting accepts
        public Type ParameterType { get; set; }
        /// Internal name of the setting
        public string InternalName { get; set; }
        /// Order setting should appear in (since attributes are returned in no particular order)
        public int Order { get; set; }
        /// SortKey, which is the Order prepended to the name
        public string SortKey { get { return string.Format("{0:D2}{1}", Order, Name); } }


        /// Constructor; name and description are mandatory, type defaults to
        /// boolean, default value to null (false)
        public SettingAttribute(string name, string desc)
            : base(desc)
        {
            Name = name;
            InternalName = name;
            ParameterType = typeof(bool);
            _hasDefaultValue = true;
        }
    }


    /// <summary>
    /// Used to define a single setting that has one or more dynamic names/values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DynamicSettingAttribute : SettingAttribute
    {
        /// Constructor; description is mandatory, type defaults to
        /// boolean, default value to null (false)
        public DynamicSettingAttribute(string label, string desc)
            : base("<" + label + ">", desc)
        {
            ParameterType = typeof(bool);
            _hasDefaultValue = true;
        }
    }

}
