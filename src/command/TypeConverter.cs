using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Command arguments will often be provided in the form of strings, which
    /// must then be converted to the command argument type. This interface
    /// defines the methods that must be implemented by a class that wishes to
    /// register itself as a type converter from strings to a target type.
    /// </summary>
    public interface ITypeConverter
    {
        /// <summary>
        /// Returns true if this object can convert strings to the target type.
        /// </summary>
        bool CanConvert(Type type);

        /// <summary>
        /// Perform a conversion of the specified string value to the target
        /// type.
        /// </summary>
        object ConvertTo(string val, Type type);
    }



    /// <summary>
    /// Provides a general purpose and extensible ITypeConverter implementation.
    /// String-to-object type conversions can be registered in an instance of
    /// this class via lambda functions. Additionally, default mappings are
    /// provided for:
    /// - string, int, double, and bool primitives
    /// - DateTime objects
    /// - enums
    /// - Arrays and IEnumerable<> of any of the above
    /// </summary>
    public class PluggableTypeConverter : ITypeConverter
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Collection of mapping functions for converting strings to target types
        private Dictionary<Type, Func<string, Type, object>> _maps;

        /// <summary>
        /// Returns the conversion function used to convert strings to the
        /// specified type.
        /// </summary>
        public Func<string, Type, object> this[Type type] {
            get { return _maps[type]; }
            set { _maps[type] = value; }
        }


        /// Default constructor, which registers default conversions
        public PluggableTypeConverter() : this(true) { }


        /// <summary>
        /// Construct a new instance, and registers default conversions if
        /// includeDefaults is true.
        /// </summary>
        public PluggableTypeConverter(bool includeDefaults) {
            _maps = new Dictionary<Type, Func<string, Type, object>>();

            if(includeDefaults) {
                this[typeof(string)] = (val, type) => val;
                this[typeof(int)] = (val, type) => int.Parse(val);
                this[typeof(double)] = (val, type) => double.Parse(val);
                this[typeof(bool)] = (val, type) =>
                    new Regex("^t(rue)?|y(es)?$", RegexOptions.IgnoreCase).IsMatch(val);
                this[typeof(DateTime)] = (val, type) => DateTime.Parse(val);
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
            else if(type.IsGenericType && type.GetGenericTypeDefinition() ==  typeof(IEnumerable<>)) {
                var args = type.GetGenericArguments();
                if(args.Length == 1) {
                    return args[0].IsEnum || _maps.ContainsKey(args[0]);
                }
            }
            else if(type.IsEnum) {
                return true;
            }
            return _maps.ContainsKey(type);
        }


        /// <summary>
        /// Returns the result of converting the supplied string value to the
        /// target type.
        /// </summary>
        public object ConvertTo(string value, Type type)
        {
            if(CanConvert(type)) {
                if(_maps.ContainsKey(type)) {
                    return _maps[type](value, type);
                }
                else if(type.IsEnum) {
                    return ConvertEnum(value, type);
                }
                else if(type.IsArray) {
                    return ConvertArray(value, type.GetElementType());
                }
                else if(type.IsGenericType && type.GetGenericTypeDefinition() ==  typeof(IEnumerable<>)) {
                    return ConvertArray(value, type.GetGenericArguments()[0]);
                }
            }
            throw new ArgumentException(
                    string.Format("No conversion is registered for type {0}", type));
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
                // TODO: Hide EnumPrefix option on parameters/settings
                throw new ArgumentException(string.Format("Invalid value '{0}' specified for {1}." +
                            "Valid values are: {2}", val, type,
                            string.Join(", ", Enum.GetNames(type))), ex);
            }
        }


        /// Converts a string to an array of objects
        protected object ConvertArray(string val, Type type)
        {
            string[] vals = val.Split(',');     // TODO: Handle quoted values
            var obj = Array.CreateInstance(type, vals.Length);
            for(int i = 0; i < vals.Length; ++i) {
                obj.SetValue(ConvertTo(vals[i], type), i);
            }
            return obj;
        }

    }

}
