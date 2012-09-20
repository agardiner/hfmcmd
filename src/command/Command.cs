using System;
using System.Reflection;
using System.Collections.Generic;

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

        /// The MethodInfo object describing the underlying method.
        internal readonly MethodInfo MethodInfo;
        /// The class on which this Command is found.
        public readonly Type Type;
        /// List of CommandParamter definitions describing the parameters to
        /// this command.
        public readonly List<CommandParameter> Parameters = new List<CommandParameter>();
        /// Link to the associated Factory definition if this Command is also a
        /// Factory.
        protected internal Factory _factory;
        /// The Command description
        public string Description;

        // Properties

        /// The name of the Command
        public string Name { get { return MethodInfo.Name; } }
        /// The return type of this Command
        public Type ReturnType { get { return MethodInfo.ReturnType; } }
        /// True if this Command is also a Factory for objects
        public bool IsFactory { get { return _factory != null; } }
        /// Link to the associated Factory instance if this Command is a Factory
        public Factory Factory { get { return _factory; } }


        // Constructor
        public Command(Type t, MethodInfo mi, CommandAttribute attr)
        {
            this.MethodInfo = mi;
            this.Type = t;
            this.Description = attr.Description;

            _log.DebugFormat("Found command {0}", this.Name);

            foreach(var pi in mi.GetParameters()) {
                var param = new CommandParameter(pi);
                _log.DebugFormat("Found parameter {0}", param);
                this.Parameters.Add(param);
            }
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
        /// Type of the setting values
        Type ParameterType { get; }
        /// Description for the setting
        string Description { get; }
        /// Whether the setting value is sensitive, and hence should be masked
        bool IsSensitive { get; }
        /// Whether the setting has a default value (since null may be that default)
        bool HasDefaultValue { get; }
        /// The default value (if HasDefaultValue is true)
        object DefaultValue { get; }

        /// Is the setting current for the specified version?
        bool IsCurrent(string version);
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
        /// Returns the ParameterAttribute associated with this parameter (if any)
        public ParameterAttribute ParameterAttribute { get { return _paramAttribute; } }
        /// Returns the description of the parameter
        public string Description {
            get {
                return _paramAttribute != null ? _paramAttribute.Description : null;
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
        public bool IsCollection
        {
            get { return typeof(ISettingsCollection).IsAssignableFrom(ParameterType); }
        }

        /// Returns true if the parameter is current
        public bool IsCurrent(string version)
        {
            return _paramAttribute != null ? _paramAttribute.IsCurrent(version) : true;
        }


        /// Constructor
        public CommandParameter(ParameterInfo pi)
        {
            _name = pi.Name;
            _paramType = pi.ParameterType;
            _paramAttribute = Attribute.GetCustomAttribute(pi, typeof(ParameterAttribute)) as ParameterAttribute;
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
        object this[string key]
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the default value for a single setting in the collection.
        /// </summary>
        object DefaultValue(string key);

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

        protected readonly MemberInfo _memberInfo;
        /// The Type of the object that is returned by this factory
        public readonly Type ReturnType;
        /// Reference to the Command definition (if this factory is also a command)
        protected internal Command _command;

        /// Public properties

        public string          Name { get { return _memberInfo.Name; } }
        public bool            IsCommand { get { return _command != null; } }
        public Command         Command { get { return _command; } }
        public bool            IsConstructor { get { return _memberInfo is ConstructorInfo; } }
        public ConstructorInfo Constructor { get { return _memberInfo as ConstructorInfo; } }
        public bool            IsProperty { get { return _memberInfo is PropertyInfo; } }
        public PropertyInfo    Property { get { return _memberInfo as PropertyInfo; } }
        /// The Type of the class on which this Factory is declared
        public Type            DeclaringType { get { return _memberInfo.DeclaringType; } }


        /// Constructor
        public Factory(MemberInfo mi)
        {
            this._memberInfo = mi;
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
                return string.Format("Factory method {0} for {1}", Name, ReturnType);
            }
            else if(_memberInfo is PropertyInfo) {
                return string.Format("Factory property {0} for {1}", Name, ReturnType);
            }
            else if(_memberInfo is ConstructorInfo) {
                return string.Format("Factory constructor for {0}", ReturnType);
            }
            else return base.ToString();
        }
    }

}
