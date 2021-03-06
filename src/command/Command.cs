using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Represents a method that can be invoked by some external means.
    /// </summary>
    public class Command
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Fields

        /// The class on which this Command is found.
        public readonly Type Type;
        /// The MethodInfo object describing the underlying method.
        internal readonly MethodInfo MethodInfo;
        /// The CommandAttribute that tagged this as a Command.
        public readonly CommandAttribute CommandAttribute;
        /// List of CommandParamter definitions describing the parameters to
        /// this command.
        public readonly List<CommandParameter> Parameters = new List<CommandParameter>();
        /// Link to the associated Factory definition if this Command is also a
        /// Factory.
        protected internal Factory _factory;
        /// The Command alias (if any)
        public string Alias { get { return CommandAttribute.Alias; } }
        /// The Command description
        public string Description { get { return CommandAttribute.Description; } }
        /// Whether the command is versioned
        public bool IsVersioned { get { return CommandAttribute.IsVersioned; } }

        // Properties

        /// The name of the Command
        public string Name {
            get {
                return CommandAttribute.Name != null ?
                    CommandAttribute.Name :
                    MethodInfo.Name;
            }
        }
        /// The return type of this Command
        public Type ReturnType { get { return MethodInfo.ReturnType; } }
        /// True if this Command is also a Factory for objects
        public bool IsFactory { get { return _factory != null; } }
        /// Link to the associated Factory instance if this Command is a Factory
        public Factory Factory { get { return _factory; } }
        /// Count of the number of parameters that have ParameterAttributes
        public int NumUserSuppliedParameters
        {
            get {
                return Parameters.Count(p => p.HasParameterAttribute || p.IsSettingsCollection);
            }
        }


        // Constructor
        public Command(Type t, MethodInfo mi, CommandAttribute attr)
        {
            this.MethodInfo = mi;
            this.Type = t;
            this.CommandAttribute = attr;

            _log.DebugFormat("Found command {0}", this.Name);

            foreach(var pi in mi.GetParameters()) {
                var param = new CommandParameter(pi);
                _log.DebugFormat("Found parameter {0}", param);
                this.Parameters.Add(param);
            }
        }


        /// <summary>
        /// Returns true if the Command is current for the specified version
        /// </summary>
        public bool IsCurrent(Version ver)
        {
            return CommandAttribute.IsCurrent(ver);
        }


        public override string ToString()
        {
            return Name;
        }
    }



    /// <summary>
    /// Corresponds to a primitive value representing an argument, or a
    /// component of a more complex type.
    /// </summary>
    public interface ISetting
    {
        /// Name of the setting
        string Name { get; }
        /// Alternate name for the setting (e.g. plural)
        string Alias { get; }
        /// Type of the setting values
        Type ParameterType { get; }
        /// Description for the setting
        string Description { get; }
        /// Returns true if the setting has an alternate name
        bool HasAlias { get; }
        /// Whether the setting value is sensitive, and hence should be masked
        bool IsSensitive { get; }
        /// Whether the setting has a default value (since null may be that default)
        bool HasDefaultValue { get; }
        /// The default value (if HasDefaultValue is true)
        object DefaultValue { get; }
        /// Returns true if the setting has a Since and/or Deprecated version
        bool IsVersioned { get; }

        /// Is the setting current for the specified version?
        bool IsCurrent(Version version);
        /// Returns true if the setting has been tagged with the specified UDA
        bool HasUda(string uda);
    }



    /// <summary>
    /// Records details of a Command parameter.
    /// </summary>
    public class CommandParameter : ISetting
    {
        // Fields
        private string _name;
        private Type _paramType;
        private ParameterAttribute _paramAttribute;

        // Properties

        /// Returns the name of the parameter
        public string Name { get { return _name; } }
        /// Returns the type of the parameter's values
        public Type ParameterType { get { return _paramType; } }
        /// Returns true if the command parameter has a ParameterAttribute
        public bool HasParameterAttribute { get { return _paramAttribute != null; } }
        /// Returns the ParameterAttribute associated with this parameter (if any)
        public ParameterAttribute ParameterAttribute { get { return _paramAttribute; } }
        /// Returns the description of the parameter
        public string Description {
            get {
                return _paramAttribute != null ? _paramAttribute.Description : null;
            }
        }
        /// Returns the alternate name of the parameter
        public string Alias {
            get {
                return _paramAttribute != null ? _paramAttribute.Alias : null;
            }
        }
        /// Returns true if this parameter has an alias
        public bool HasAlias {
            get {
                return _paramAttribute != null ? _paramAttribute.HasAlias : false;
            }
        }
        /// Returns true if this parameter has a default value
        public bool HasDefaultValue {
            get {
                return _paramAttribute != null ? _paramAttribute.HasDefaultValue : false;
            }
        }
        /// Returns the default value for this parameter
        public object DefaultValue {
            get {
                return _paramAttribute != null ? _paramAttribute.DefaultValue : null;
            }
        }
        /// Returns true if this parameter value is sensitive
        public bool IsSensitive {
            get {
                return _paramAttribute != null ? _paramAttribute.IsSensitive : false;
            }
        }
        /// Returns true if this parameter is an ISettingsCollection
        public bool IsSettingsCollection
        {
            get { return typeof(ISettingsCollection).IsAssignableFrom(ParameterType); }
        }
        /// Returns true if there is a Since or Deprecated attribute
        public bool IsVersioned
        {
            get {
                return _paramAttribute != null &&
                    !(_paramAttribute.Since == null && _paramAttribute.Deprecated == null);
            }
        }


        /// Constructor
        public CommandParameter(ParameterInfo pi)
        {
            _name = pi.Name;
            _paramType = pi.ParameterType;
            _paramAttribute = Attribute.GetCustomAttribute(pi, typeof(ParameterAttribute)) as ParameterAttribute;
        }


        /// <summary>
        /// Returns true if the parameter is current
        /// </summary>
        public bool IsCurrent(Version version)
        {
            return _paramAttribute != null ? _paramAttribute.IsCurrent(version) : true;
        }


        /// <summary>
        /// Returns true if the parameter is tagged with the Uda
        /// </summary>
        public bool HasUda(string uda)
        {
            return _paramAttribute != null ? _paramAttribute.HasUda(uda) : false;
        }


        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, ParameterType);
        }

    }



    /// <summary>
    /// Defines the methods and properties required of a settings collection,
    /// which is simply a collection for what would otherwise be individual
    /// parameters to a command.  Used for commands that take an impractically
    /// large set of parameters, many of which have default values. Instead of
    /// having to define all settings as parameters to the command, a single
    /// ISettingsCollection object can be used instead.
    /// </summary>
    public interface ISettingsCollection
    {

        /// <summary>
        /// Gets or sets a single setting in the settings collection.
        /// </summary>
        object this[string key] { get; set; }
    }


    /// <summary>
    /// Marks a setting collection as possessing a dynamic set of settings, i.e.
    /// one whose members cannot be determined until run-time.
    /// </summary>
    public interface IDynamicSettingsCollection : ISettingsCollection
    {
        IEnumerable<string> DynamicSettingNames { get; }
    }


    /// <summary>
    /// Represents a constructor, method, or property that can be invoked to
    /// obtain an instance of a given type.
    /// </summary>
    public class Factory
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// The MemberInfo that holds details about the item (i.e. method,
        /// constructor, or property) that is the factory
        protected readonly MemberInfo _memberInfo;
        /// The FactoryAttribute used to identify a factory
        protected readonly FactoryAttribute _factoryAttribute;
        /// The Type of the object that is returned by this factory
        public readonly Type ReturnType;
        /// Reference to the Command definition (if this factory is also a command)
        protected internal Command _command;

        // Public properties

        /// True if the factory is also a command
        public bool            IsCommand { get { return _command != null; } }
        /// The Command object for the factory command
        public Command         Command { get { return _command; } }
        /// True if the factory is a constructor
        public bool            IsConstructor { get { return _memberInfo is ConstructorInfo; } }
        /// The ConstructorInfo for the factory constructor
        public ConstructorInfo Constructor { get { return _memberInfo as ConstructorInfo; } }
        /// True if the factory is a property on another object type
        public bool            IsProperty { get { return _memberInfo is PropertyInfo; } }
        /// The PropertyInfo for the factory property
        public PropertyInfo    Property { get { return _memberInfo as PropertyInfo; } }
        /// The Type of the class on which this Factory is declared
        public Type            DeclaringType { get { return _memberInfo.DeclaringType; } }
        /// True if the factory is an alternate (rather than primary) means of
        /// creating objects
        public bool            IsAlternate { get { return _factoryAttribute.Alternate; } }
        /// True if the factory object can be persisted across multiple commands
        public bool            IsSingleUse { get { return _factoryAttribute.SingleUse; } }


        /// Constructor
        public Factory(MemberInfo mi, FactoryAttribute fa)
        {
            _memberInfo = mi;
            _factoryAttribute = fa;
            if(mi is MethodInfo) {
                ReturnType = (mi as MethodInfo).ReturnType;
            }
            else if(mi is PropertyInfo) {
                ReturnType = (mi as PropertyInfo).PropertyType;
            }
            else if(mi is ConstructorInfo) {
                ReturnType = (mi as ConstructorInfo).DeclaringType;
            }
            _log.DebugFormat("Found factory for {0} objects", ReturnType);
        }


        public override string ToString()
        {
            if(_memberInfo is MethodInfo) {
                return string.Format("Factory method {0} for {1}", _memberInfo.Name, ReturnType);
            }
            else if(_memberInfo is PropertyInfo) {
                return string.Format("Factory property {0} for {1}", _memberInfo.Name, ReturnType);
            }
            else if(_memberInfo is ConstructorInfo) {
                return string.Format("Factory constructor for {0}", ReturnType);
            }
            else return base.ToString();
        }
    }

}
