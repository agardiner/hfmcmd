using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using log4net;
using log4net.Core;
using log4net.Appender;



namespace HFMCmd
{

    /// <summary>
    /// Defines the interface for classes that want to participate in outputting
    /// tabular information.
    /// </summary>
    public interface IOutput
    {
        /// <summary>
        /// Property for setting the current operation that is in progress.
        /// </summary>
        string Operation { get; set; }

        /// <summary>
        /// Write a line of text, using string.Format to combine multiple
        /// values into a line of text.
        /// </summary>
        void WriteLine(string format, params object[] values);

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
        void End(bool suppressFooter);

        /// <summary>
        /// Instructs the output mechanism to display some form of progress
        /// indicator, if appropriate. Progress will be measured in units up to
        /// total, which will be repeated iterations times.
        /// </summary>
        void InitProgress(string operation, int iterations, int total);

        /// <summary>
        /// Update the current progress completion ratio for the current
        /// iteration. If the IOutput implementation returns true, the
        /// operation should be aborted, if possible.
        /// </summary>
        bool SetProgress(int progress);

        /// <summary>
        /// Update the current progress iteration.
        /// </summary>
        bool IterationComplete();

        /// <summary>
        /// Indicates that the operation that was in progress is now complete.
        /// </summary>
        void EndProgress();
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
        // Reference to class logger
        public static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Characters at which line breaks can be created
        public static char[] LINEBREAKS = new char[] { '\n', '\r' };
        public static char[] WHITESPACE = new char[] { ' ', '\t' };
        public static char[] WORDBREAKS = new char[] { '-', ',', '.', ';', '#', ')', ']', '}' };


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
        /// Method for wrapping text to a certain width.
        /// </summary>
        public static List<string> WrapText(object value, int width)
        {
            List<string> lines = new List<string>();
            var sVal = value == null ? "" : value.ToString().Trim();
            var nlPos = 0;
            var wsPos = 0;
            var wbPos = 0;
            string chunk;

            while(sVal.Length > width) {
                chunk = sVal.Substring(0, width + 1);
                nlPos = chunk.IndexOfAny(LINEBREAKS);
                wsPos = chunk.LastIndexOfAny(WHITESPACE);
                wbPos = chunk.LastIndexOfAny(WORDBREAKS, width - 1);
                if(nlPos >= 0) {
                    // Break at new-line
                    lines.Add(sVal.Substring(0, nlPos).Trim());
                    sVal = sVal.Substring(nlPos+1).Trim();
                }
                else if(wsPos > 0 && wsPos > wbPos || (wbPos > 5 && wbPos - 5 < wsPos)) {
                    // Break at whitespace
                    lines.Add(sVal.Substring(0, wsPos).Trim());
                    sVal = sVal.Substring(wsPos + 1).Trim();
                }
                else if(wbPos > 0) {
                    // Break at word-break character
                    lines.Add(sVal.Substring(0, wbPos + 1));
                    sVal = sVal.Substring(wbPos + 1).Trim();
                }
                else {
                    // Force a break at width
                    lines.Add(sVal.Substring(0, width));
                    sVal = sVal.Substring(width);
                }
            }
            lines.Add(sVal);

            return lines;
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we just want to
        /// output a blank line.
        /// </summary>
        public static void WriteLine(this IOutput output)
        {
            output.WriteLine("");
        }


        /// <summary>
        /// Eases use of the IOutput interface for cases where we just want to
        /// output a single record with a single field.
        /// </summary>
        public static void WriteSingleValue(this IOutput output, object value, params object[] field)
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


        /// <summary>
        /// Convenience for the common case of not suppressing footer record
        /// </summary>
        public static void End(this IOutput output)
        {
            output.End(false);
        }


        /// <summary>
        /// Helper method for initiating an operation whose progress will be
        /// monitored. Provides defaults for iteration (1) and total (100).
        /// </summary>
        public static void InitProgress(this IOutput output, string operation)
        {
            output.InitProgress(operation, 1, 100);
        }


        /// <summary>
        /// Helper method for initiating an operation whose progress will be
        /// monitored. Provides default for total (100).
        /// </summary>
        public static void InitProgress(this IOutput output, string operation, int iterations)
        {
            output.InitProgress(operation, iterations, 100);
        }


        /// <summary>
        /// Method that can be used by IOutput implementations to determine the
        /// appropriate value to send for the cancel return value in a call to
        /// SetProgress.
        /// </summary>
        public static bool ShouldCancel()
        {
            return CommandLine.UI.Interrupted || CommandLine.UI.EscPressed;
        }
    }



    /// <summary>
    /// Implementation of IOutput that does not output - like a /dev/nul device,
    /// it ignores output sent to it. Use this when no output is desired, since
    /// a valid IOutput (non-null) IOutput instance must be supplied to many
    /// methods. Use of this class prevents methods that take an IOutput from
    /// having to check it it is non-null.
    /// </summary>
    public class NullOutput : IOutput
    {
        public static NullOutput Instance = new NullOutput();

        public string Operation { get; set; }

        public void WriteLine(string format, params object[] values) { }
        public void SetHeader(params object[] fields) { }
        public void WriteRecord(params object[] values) { }
        public void End(bool suppress) { }
        public void InitProgress(string operation, int iterations) { }
        public void InitProgress(string operation, int iterations, int total) { }
        public bool SetProgress(int progress) { return OutputHelper.ShouldCancel(); }
        public bool IterationComplete() { return OutputHelper.ShouldCancel(); }
        public void EndProgress() { }
    }



    /// <summary>
    /// Base class for IOutput implementations that output data in fixed width
    /// format.
    /// </summary>
    public abstract class FixedWidthOutput : IOutput
    {
        /// Maximum width for a line of output
        private int _maxWidth = -1;
        private int _indentWidth = 0;
        private string _indentString = "";

        /// Current operation
        public string Operation { get; set; }
        /// Level of indentation to use
        public int IndentWidth
        {
            get { return _indentWidth; }
            set {
                _indentWidth = value;
                _indentString = new String(' ', _indentWidth);
            }
        }
        public string IndentString { get { return _indentString; } }
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

        /// Whether current operation has been cancelled
        protected bool _cancelled;
        /// Total number of iterations of progress operation to be completed
        protected int _totalIterations;
        /// Completed iterations
        protected int _iteration;
        /// Total number of steps in a single iteration of the progress operation
        protected int _total;
        /// Currently completed number of progress steps in the current iteration
        protected int _progress;


        public abstract void WriteLine(string format, params object[] values);


        public virtual void SetHeader(params object[] fields)
        {
            if(fields != null && fields.Length > 0) {
                _fieldNames = OutputHelper.GetFieldNamesAndWidths(fields, out _widths);
                var total = IndentWidth;
                for(var i = 0; i < _widths.Length; total += _widths[i++] + FieldSeparator.Length) {
                    // Last field gets special treatment
                    if(i == _widths.Length - 1 && MaxWidth > 0 && total < MaxWidth &&
                       (_widths[i] == 0 ||_widths[i] > (MaxWidth - total))) {
                        _widths[i] = MaxWidth - total;
                    }
                    else if(_widths[i] == 0) {
                        _widths[i] = DefaultFieldWidth;
                    }
                }
                var formats = _fieldNames.Select((field, i) =>
                        _widths[i] > 0 ?
                            string.Format("{{{0},{1}}}", i, _widths[i] * -1) :
                            "{0}"
                        ).ToArray();
                _format = new String(' ', IndentWidth) + string.Join(FieldSeparator, formats);
            }
            else {
                _format = null;
            }
            _records = 0;
        }


        public virtual void WriteRecord(params object[] values)
        {
            _records++;
        }


        // Default no-op implementation
        public virtual void End(bool suppress)
        {
        }


        /// <summary>
        /// Wraps record values according to the current column widths, and
        /// returns a List of lines, formatted to line up correctly in the
        /// fields.
        /// </summary>
        public List<string> Wrap(params object[] values)
        {
            List<string>[] fields = new List<string>[values.Length];
            var i =0;
            var lineCount = 0;

            foreach(var val in values) {
                fields[i] = OutputHelper.WrapText(val, _widths[i]);
                lineCount = Math.Max(lineCount, fields[i].Count);
                i++;
            }

            List<string> lines = new List<string>(lineCount);

            var line = 0;
            string[] record = new string[values.Length];
            while(line < lineCount) {
                record = new string[values.Length];
                for(i = 0; i < values.Length; ++i) {
                    if(fields[i].Count > line) {
                        record[i] = fields[i][line];
                    }
                }
                lines.Add(string.Format(_format, record));
                line++;
            }
            return lines;
        }


        public virtual void InitProgress(string operation, int iterations, int total)
        {
            Operation = operation;
            _totalIterations = iterations;
            _iteration = 0;
            _total = total;
            _progress = 0;
            _cancelled = false;
        }


        public virtual bool SetProgress(int progress)
        {
            if(Operation == null) {
                throw new InvalidOperationException("SetProgress called when no operation was supposed to be in progress");
            }
            _progress = progress;
            _cancelled = _cancelled || OutputHelper.ShouldCancel();
            return _cancelled;
        }


        public virtual bool IterationComplete()
        {
            if(Operation == null) {
                throw new InvalidOperationException("IterationComplete called when no operation was supposed to be in progress");
            }
            _iteration++;
            _progress = 0;
            _cancelled = _cancelled || OutputHelper.ShouldCancel();
            return _cancelled;
        }


        public virtual void EndProgress()
        {
            if(_cancelled) {
                WriteLine("{0} cancelled", Operation);
            }
            Operation = null;
        }


        protected virtual int CompletionPct()
        {
            int pct = 0;
            if(_totalIterations * _total > 0) {
                pct = (_iteration * _total + _progress) * 100 / (_totalIterations * _total);
                if (pct < 0) {
                    pct = 0;
                }
                else if (pct > 100) {
                    pct = 100;
                }
            }
            return pct;
        }

    }



    /// <summary>
    /// Sends output to the log.
    /// </summary>
    public class LogOutput : FixedWidthOutput
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Minimum interval that must elapse before we will consider logging a
        /// progress update
        public static int MIN_LOG_INTERVAL = 15;
        /// Maximum interval that should pass without logging a progress update
        public static int MAX_LOG_INTERVAL = 300;
        /// Minimum progress that must be made before a new log message is generated
        public static int MIN_PROGRESS_INCREMENT = 10;

        // Time the last progress log message was generated
        protected DateTime _lastLog;


        /// Constructor
        public LogOutput()
        {
            IndentWidth = 4;
        }


        public override void WriteLine(string format, params object[] values)
        {
            _log.InfoFormat(format, values);
        }



        public override void SetHeader(params object[] fields)
        {
            base.SetHeader(fields);

            // Display the field headers if there is more than one field
            if(fields.Length > 1) {
                _log.InfoFormat(_format, _fieldNames.Select((field, i) => new String('-', _widths[i])).ToArray());
                _log.InfoFormat(_format, _fieldNames);
                _log.InfoFormat(_format, _fieldNames.Select((field, i) => new String('-', _widths[i])).ToArray());
            }
        }


        public override void WriteRecord(params object[] values)
        {
            if(_format != null) {
                foreach(var line in Wrap(values)) {
                    _log.Info(line);
                }
            }
            else if(values.Length > 0) {
                foreach(var line in values) {
                    _log.Info(line);
                }
            }
            else {
                _log.Info("");
            }
            ++_records;
        }


        public override void End(bool suppress)
        {
            if(!suppress && _widths != null && _widths.Length > 0 && _records > 5) {
                _log.InfoFormat("{0} records output", _records);
            }
        }


        public override void InitProgress(string operation, int iterations, int total)
        {
            base.InitProgress(operation, iterations, total);
            _lastLog = DateTime.MinValue;
        }


        public override bool IterationComplete()
        {
            int lastPct = CompletionPct();
            base.IterationComplete();
            UpdateCompletion(lastPct);

            return _cancelled;
        }


        public override bool SetProgress(int progress)
        {
            int lastPct = CompletionPct();
            base.SetProgress(progress);
            int pct = CompletionPct();
            UpdateCompletion(lastPct);

            return _cancelled;
        }


        protected void UpdateCompletion(int lastPct)
        {
            int pct = CompletionPct();
            // Log progress messages for (at most) each 10% of progress
            // Handle cases where progress appears to go backwards e.g.
            // due to several passes being made in an EA extract.

            // Log progress only if sufficient (but not too much) time
            // has passed, and sufficient progress has been made
            if((DateTime.Now.AddSeconds(-MAX_LOG_INTERVAL) > _lastLog) ||
               (DateTime.Now.AddSeconds(-MIN_LOG_INTERVAL) > _lastLog &&
                (pct < lastPct || (pct - lastPct) >= 10))) {
                _log.InfoFormat("{0} {1}% complete", Operation.Trim(), pct);
                _lastLog = DateTime.Now;
            }
        }
    }



    /// <summary>
    /// Sends output to the console
    /// </summary>
    public class ConsoleOutput : FixedWidthOutput
    {

        // Characters used to simulate a spinner
        private static readonly char[] _spinner = new char[] { '|', '/', '-', '\\' };
        // Size of the progress bar
        private static int BAR_SIZE = 50;

        // Reference to CommandLine.UI, used to render our output
        private CommandLine.UI _cui;
        // Counter used to cause spinner to animate
        private int _spin;


        public ConsoleOutput(CommandLine.UI cui)
        {
            _cui = cui;
            IndentWidth = 7;
            MaxWidth = cui.ConsoleWidth;
        }


        public override void WriteLine(string format, params object[] values)
        {
            if(Operation != null && _spin > 0) {
                _cui.ClearLine();
            }
            if(values.Length == 0) {
                _cui.WriteLine(format);
            }
            else {
                _cui.WriteLine(string.Format(format, values));
            }

            if(Operation != null && _spin > 0) {
                RenderProgressBar();
            }
        }


        public void Write(string text)
        {
            if(Operation != null && _spin > 0) {
                _cui.ClearLine();
            }
            _cui.Write(text);

            if(Operation != null && _spin > 0) {
                RenderProgressBar();
            }
        }


        public override void SetHeader(params object[] fields)
        {
            base.SetHeader(fields);

            _cui.WriteLine();
            if(fields.Length > 1) {
                _cui.WriteLine(string.Format(_format, _fieldNames));
                _cui.WriteLine(string.Format(_format, _fieldNames.Select(
                            (field, i) => new String('-', _widths[i])).ToArray()));
            }
        }


        public override void WriteRecord(params object[] values)
        {
            if(_format != null) {
                foreach(var line in Wrap(values)) {
                    _cui.WriteLine(line);
                }
            }
            else if(values.Length > 0) {
                foreach(var line in values) {
                    _cui.WriteLine(IndentString + line.ToString());
                }
            }
            else {
                _cui.WriteLine("");
            }
            ++_records;
        }


        public override void End(bool suppress)
        {
            _cui.WriteLine();
            if(!suppress && _widths != null && _widths.Length > 0 && _records > 5) {
                _cui.WriteLine(IndentString + string.Format("{0} records output", _records));
                _cui.WriteLine();
            }
        }


        public override void InitProgress(string operation, int iterations, int total)
        {
            base.InitProgress(operation, iterations, total);
            _spin = 0;
        }


        public override bool SetProgress(int progress)
        {
            base.SetProgress(progress);
            RenderProgressBar();
            return _cancelled;
        }


        public override bool IterationComplete()
        {
            base.IterationComplete();
            RenderProgressBar();
            return _cancelled;
        }


        public override void EndProgress()
        {
            base.EndProgress();
            _cui.ClearLine();
        }


        protected void RenderProgressBar()
        {
            // Determine which character to display next to simulate spinning
            char spin = _spinner[_spin++ % _spinner.Length];

            // Make sure percentage is within range of 0 to 100
            int pct = CompletionPct();

            // Build up the progress bar
            var sb = new StringBuilder(Operation.Length + BAR_SIZE + 20);
            sb.Append(Operation);
            sb.Append(' ');
            sb.Append(spin);
            sb.Append("  [");
            var barMid = sb.Length + (BAR_SIZE / 2);
            for(int i = 1; i <= BAR_SIZE; ++i) {
                if (i * 100 / BAR_SIZE <= pct) {
                    sb.Append('=');
                }
                else {
                    sb.Append(' ');
                }
            }
            sb.Append("]  ");
            if(_cancelled) {
                sb.Append("Cancelling...");
            }
            else {
                sb.Append("(Esc to cancel)");
            }

            // Now place the percentage complete inside the bar
            var pctStr = pct.ToString() + "%";
            if (pctStr.Length > 2) {
                barMid--;
            }
            sb.Remove(barMid, pctStr.Length);
            sb.Insert(barMid, pctStr);

            _cui.ClearLine();
            _cui.Write(sb.ToString());
        }

    }



    /// <summary>
    /// A more intelligent ConsoleAppender, which takes account of the
    /// console width and wraps messages at word breaks. Also integrates
    /// better with progress monitoring.
    /// </summary>
    public class ConsoleAppender : AppenderSkeleton
    {
        ConsoleOutput _console;

        public ConsoleAppender(ConsoleOutput co)
        {
            _console = co;
        }

        protected override void Append(LoggingEvent logEvent)
        {
            _console.Write(RenderLoggingEvent(logEvent));
        }
    }



}
