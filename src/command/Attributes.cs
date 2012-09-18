using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Define an attribute which will be used to set descriptions for Commands
    /// and CommandParameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter,
     AllowMultiple = false)]
    public class DescriptionAttribute : Attribute
    {
        public string Description;

        public DescriptionAttribute(string desc)
        {
            this.Description = desc;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
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
    /// Used to define the possible settings that can appear in an
    /// ISettingsCollection subclass. These often change between versions, so
    /// Since and Deprecated arguments are supported.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SettingAttribute : Attribute, ISetting
    {
        /// Name of the setting
        public string Name { get; set; }
        /// Description of the setting
        public string Description { get; set; }
        /// The Type of value which the setting accepts
        public Type ParameterType { get; set; }
        /// Whether the setting is sensitive, and should be masked
        public bool IsSensitive { get; set; }
        /// Whether the setting has a DefaultValue; settings default to true
        public bool HasDefaultValue { get; set; }
        /// Default value for setting (may be null, even if this is not the
        /// default)
        // TODO: Do we need another field to indicate if default is available?
        public object DefaultValue { get; set; }
        /// Version in which the setting was introduced
        public string Since { get; set; }
        /// Version in which the setting was deprecated
        public string Deprecated { get; set; }

        public SettingAttribute()
        {
            ParameterType = typeof(bool);
            HasDefaultValue = true;
        }

        public SettingAttribute(string name)
        {
            Name = name;
            ParameterType = typeof(bool);
            HasDefaultValue = true;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to specify default values for
    /// optional parameters on a Command.
    /// A DefaultValue attribute is used instead of default values on the
    /// actual method, since a) default values are only available from v4 of
    /// .Net, and b) they have restrictions on where they can appear (e.g. only
    /// at the end of the list of parameters).
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class DefaultValueAttribute : Attribute
    {
        public object Value;

        public DefaultValueAttribute(object val)
        {
            this.Value = val;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to tag parameters that contain
    /// sensitive information such as passwords. These will be masked if logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class SensitiveValueAttribute : Attribute
    {
    }

}
