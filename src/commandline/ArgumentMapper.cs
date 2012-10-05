using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

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

}
