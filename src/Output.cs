using System;
using System.Collections.Generic;
using System.Linq;

using log4net;



namespace HFMCmd
{

    public interface IOutput
    {
        void SetFields(params string[] fields);
        void WriteRecord(params string[] values);
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

        private int[] _widths;
        private string _format;

        public void SetFields(params string[] fields)
        {
            _widths = fields.Select(field => FieldWidths.ContainsKey(field) ? FieldWidths[field] :
                        DefaultFieldWidth).ToArray();
            var formats = fields.Select((field, i) => string.Format("{{{0},-{1}}}", i, _widths[i])).ToArray();
            _format = new String(' ', IndentWidth) + string.Join(" ", formats);

            // Display the field headers if there is more than one field
            if(fields.Length > 1) {
                _log.InfoFormat(_format, fields);
                _log.InfoFormat(_format, fields.Select((field, i) => new String('-', _widths[i])).ToArray());
            }
        }


        public void WriteRecord(params string[] values)
        {
            _log.InfoFormat(_format, values);
        }
    }

}
