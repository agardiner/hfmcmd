using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using log4net;



namespace HFMCmd
{

    /// <summary>
    /// Defines the interface for classes that want to participate in outputting
    /// tabular information.
    /// </summary>
    public interface IOutput
    {
        /// <summary>
        /// Called before a table of information is output; identifies the names
        /// of the fields that will be output (and implicitly, how many fields
        /// there will be per record). Defined as an array of objects, since
        /// field names may be followed by an optional int width specification.
        /// As such, implementations that don't need field widths should ignore
        /// non-string fields.
        /// A special case occurs if this method is called with no fields; in
        /// this case, the output should be formatted as a plain text with no
        /// headers.
        /// </summary>
        void SetHeader(params object[] fields);

        /// <summary>
        /// Called for each record that is to be output.
        /// </summary>
        void WriteRecord(params object[] values);

        /// <summary>
        /// Marker method called to indicate completion of output for the current
        /// table.
        /// </summary>
        void End();
    }


    /// <summary>
    /// Helper class providing convenience methods that can be used with any
    /// IOutput implementations to ease output of common cases, such as:
    /// - a blank line
    /// - a single line with no field information
    /// - a single record with a single field
    /// - a line per object in an IEnumerable
    /// </summary>
    public static class OutputHelper
    {

        /// <summary>
        /// Converts the object[] passed to SetHeader into field names and
        /// widths. The field names are returned as a string[], while the field
        /// widths are returned in an out int[].
        /// </summary>
        public static string[] GetFieldNamesAndWidths(object[] fields, out int[] widths)
        {
            int widthIdx = -1;

            string[] names = fields.OfType<string>().ToArray();
            widths = new int[names.Length];
            for(var i = 0; i < fields.Length; ++i) {
                if(fields[i].GetType() == typeof(string)) {
                    widthIdx++;
                }
                else if(fields[i].GetType() == typeof(int) && widthIdx >= 0 && widthIdx < names.Length) {
                    widths[widthIdx] = (int)fields[i];
                }
                else {
                    throw new ArgumentException(string.Format("Invalid field specifier: {0}", fields[i]));
                }
            }

            return names;
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we just want to
        /// output a single line of text.
        /// </summary>
        public static void WriteLine(this IOutput output, params object[] values)
        {
            output.SetHeader();
            output.WriteRecord(values);
            output.End();
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we just want to
        /// output a single record with a single field.
        /// </summary>
        public static void WriteSingleValue(this IOutput output, string field, object value)
        {
            output.SetHeader(field);
            output.WriteRecord(value);
            output.End();
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we just want to
        /// output a single record with multiple fields.
        /// </summary>
        public static void WriteSingleRecord(this IOutput output, params object[] values)
        {
            output.WriteRecord(values);
            output.End();
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we want to output
        /// a collection of records with a single column.
        /// </summary>
        public static void WriteEnumerable(this IOutput output, IEnumerable enumerable, params object[] header)
        {
            output.SetHeader(header);
            foreach(var item in enumerable) {
                output.WriteRecord(item);
            }
            output.End();
        }
    }


    public class LogOutput : IOutput
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Level of indentation to use
        public int IndentWidth = 4;
        /// Default field width if no width specified for a given field name
        public int DefaultFieldWidth = 20;
        /// Field widths to use for fields with a matching name
        public readonly Dictionary<string, int> FieldWidths =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private int _records;
        private int[] _widths;
        private string _format;

        public void SetHeader(params object[] fields)
        {
            _records = 0;
            var headers = OutputHelper.GetFieldNamesAndWidths(fields, out _widths);
            var formats = fields.Select((field, i) => string.Format("{{{0},-{1}}}", i, _widths[i])).ToArray();
            _format = new String(' ', IndentWidth) + string.Join(" ", formats);

            // Display the field headers if there is more than one field
            if(fields.Length > 1) {
                _log.InfoFormat(_format, fields);
                _log.InfoFormat(_format, fields.Select((field, i) => new String('-', _widths[i])).ToArray());
            }
        }


        public void WriteRecord(params object[] values)
        {
            _log.InfoFormat(_format, values);
            _records++;
        }

        public void End()
        {
            if(_widths.Length > 0 && _records > 5) {
                _log.InfoFormat("{0} records output", _records);
            }
        }
    }

}
