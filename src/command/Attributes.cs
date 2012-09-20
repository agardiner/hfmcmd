using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
        /// Description of the command (mandatory)
        public readonly string Description;

        public CommandAttribute(string desc) {
            Description = desc;
        }
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
    }



    /// </summary>
    /// Define an attribute that can be used to tag a method, property, or
    /// constructor as an alternate source of new instances of the class returned
    /// by the member. This information will be used by Context objects to
    /// determine how to obtain objects of the required type when invoking a
    /// Command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property,
     AllowMultiple = false)]
    public class AlternateFactoryAttribute : Attribute
    {
    }



    /// <summary>
    /// Abstract base class for Parameter and Setting attributes.
    /// </summary>
    public abstract class AbstractSettingAttribute : Attribute
    {
        protected object _defaultValue = null;
        protected bool   _hasDefaultValue = false;

        /// Description of the setting
        public string Description { get; set; }
        /// Whether the parameter has a DefaultValue (since the default may be null)
        public bool   HasDefaultValue { get { return _hasDefaultValue; } }
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
        /// Whether the setting has a DefaultValue; settings default to true
        public bool   IsSensitive { get; set; }
        /// The constant prefix of all values in the enum which can be omitted
        public string EnumPrefix { get; set; }
        /// Version in which the setting was introduced
        public string Since { get; set; }
        /// Version in which the setting was deprecated
        public string Deprecated { get; set; }


        /// Checks if the setting is current for a specified version, based on
        // the Since and/or Deprecated settings.
        public bool IsCurrent(string version)
        {
            bool current = true;
            if(Since != null || Deprecated != null) {
                var ver = ConvertVersionStringToNumber(version);
                var from = Since != null ? ConvertVersionStringToNumber(Since) : ver;
                var to = Deprecated != null ? ConvertVersionStringToNumber(Deprecated) : ver + 1;
                current = ver >= from && ver < to;
            }
            return current;
        }


        private int ConvertVersionStringToNumber(string ver)
        {
            var parts = ver.Split('.');
            int verNum = 0;

            for(var i = 0; i < 4; i++) {
                verNum = verNum * 100 + (i < parts.Length ? int.Parse(parts[i]) : 0);
            }
            return verNum;
        }

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
    public class ParameterAttribute : AbstractSettingAttribute
    {
        public ParameterAttribute(string desc) {
            Description = desc;
        }
    }



    /// <summary>
    /// Used to define the possible settings that can appear in an
    /// ISettingsCollection subclass. These often change between versions, so
    /// Since and Deprecated arguments are supported.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SettingAttribute : AbstractSettingAttribute, ISetting
    {
        /// Name of the setting
        public string Name { get; set; }
        /// The Type of value which the setting accepts
        public Type ParameterType { get; set; }


        public SettingAttribute(string name)
        {
            Name = name;
            ParameterType = typeof(bool);
            _hasDefaultValue = true;
        }
    }

}
