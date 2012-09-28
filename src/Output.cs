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



    /// <summary>
    /// Base class for IOutput implementations that output data in fixed width
    /// format.
    /// </summary>
    public abstract class FixedWidthOutput : IOutput
    {
        /// Maximum width for a line of output
        private int _maxWidth = -1;

        /// Level of indentation to use
        public int IndentWidth = 0;
        /// String to use as a field separator
        public string FieldSeparator = " ";
        /// Default field width if no width specified for a field
        public int DefaultFieldWidth = 20;
        /// Maximum width to constrain a line of output to. If the content
        /// exceeds this length, it will be wrapped. A value of -1 indicates
        /// no maximum width.
        public int MaxWidth {
            get { return _maxWidth; }
            set { _maxWidth = value >= 50 ? value : -1; }
        }

        /// Names of the current fields
        protected string[] _fieldNames;
        /// Actual widths to be used for subsequent calls to WriteRecord
        protected int[] _widths;
        /// Format string used to format fields into a line of text
        protected string _format;
        /// Number of records output in current table
        protected int _records;


        public virtual void SetHeader(params object[] fields)
        {
            if(fields.Length > 0) {
                _fieldNames = OutputHelper.GetFieldNamesAndWidths(fields, out _widths);
                var total = IndentWidth;
                for(var i = 0; i < _widths.Length; total += _widths[i++] + FieldSeparator.Length) {
                    // Last field gets special treatment
                    if(i == _widths.Length - 1 && MaxWidth > 0 && total < MaxWidth) {
                        _widths[i] = MaxWidth - total;
                    }
                    else if(_widths[i] == 0) {
                        _widths[i] = DefaultFieldWidth;
                    }
                }
                var formats = _fieldNames.Select((field, i) =>
                        string.Format("{{{0},-{1}}}", i, _widths[i])).ToArray();
                _format = new String(' ', IndentWidth) + string.Join(FieldSeparator, formats);
            }
            else {
                _format = null;
            }
            _records = 0;
        }


        public List<string> Wrap(params object[] values)
        {
            List<string> lines = new List<string>();
            return lines;
        }

        public virtual void WriteRecord(params object[] values)
        {
            _records++;
        }

        public virtual void End()
        {
        }

    }



    public class LogOutput : FixedWidthOutput
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// Constructor
        public LogOutput()
        {
            IndentWidth = 4;
        }


        public override void SetHeader(params object[] fields)
        {
            base.SetHeader(fields);

            // Display the field headers if there is more than one field
            if(fields.Length > 1) {
                _log.InfoFormat(_format, _fieldNames);
                _log.InfoFormat(_format, _fieldNames.Select((field, i) => new String('-', _widths[i])).ToArray());
            }
        }


        public override void WriteRecord(params object[] values)
        {
            if(_format != null) {
                _log.InfoFormat(_format, values);
            }
            else {
                foreach(var line in values) {
                    _log.Info(line);
                }
            }
            ++_records;
        }

        public override void End()
        {
            if(_widths != null && _widths.Length > 0 && _records > 5) {
                _log.InfoFormat("{0} records output", _records);
            }
        }
    }

}
