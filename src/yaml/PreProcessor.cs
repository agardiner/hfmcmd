using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

using log4net;


namespace YAML
{

    /// <summary>
    /// Implements a class that pre-processes a YAML file and handles
    /// pre-processor directives to include other files, set variables, and
    /// handle variable substitution. The pre-processor also strips comments
    /// from the contents fed back to the parser.
    /// </summary>
    public class Preprocessor : IDisposable
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // The dictionary holding variable names and values
        protected Dictionary<string, object> _variables;
        // A Stack holding the names of open files
        protected Stack<string> _files = new Stack<string>();
        // A Stack holding the current lines in each open file
        protected Stack<int> _lines = new Stack<int>();
        // A Stack holding the StreamReader for each open file
        protected Stack<StreamReader> _streams = new Stack<StreamReader>();
        // Flag indicating whether we are processing YAML at the moment
        protected Stack<bool> _process = new Stack<bool>(new bool[] {true});


        /// Accessor for the currently processing file.
        public string File { get { return _files.Peek(); } }
        /// Accessor for the currently processing line number.
        public int Line { get { return _lines.Peek(); } }


        /// <summary>
        /// Constructs a YAML Preprocessor instance.
        /// </summary>
        /// <param name="file">The source file that will drive the YAML parsing
        /// process. Note that this file may include other files, but all this
        /// is transparent to the YAMLParser.</param>
        /// <param name="variables">A Variables collection of variable names
        /// and values. The Variables collection may be modified by the
        /// Preprocessor in response to preprocessor directives to set/unset
        /// variables etc.</param>
        public Preprocessor(string file, Dictionary<string, object> variables)
        {
            _variables = variables;
            IncludeFile(file);
        }


        /// <summary>
        /// Reads the next line of input from the YAML source.
        /// </summary>
        /// <returns>A pre-processed line of text, or null if all YAML has been
        /// consumed. Note that this routine may return blank lines, which
        /// should simply be skipped by the YAMLParser.</returns>
        public string ReadLine()
        {
            // Get the next line of input from the current source file
            string line = _streams.Peek().ReadLine();

            // If file is completed, close it and return to any enclosing file
            while(line == null && _streams.Count > 0) {
                _streams.Pop().Close();
                _lines.Pop();
                _files.Pop();

                if (_streams.Count > 0) {
                    line = _streams.Peek().ReadLine();
                }
            }

            // No more lines to read?
            if(line == null) return line;

            // Update current line number and perform any variable
            // substitution on the line
            _lines.Push(_lines.Pop() + 1);
            if(line.IndexOf(" #") >= 0) {
                // Ignore any comments
                line = line.Substring(0, line.IndexOf(" #"));
            }
            if(_process.Peek()) {
                line = SubstituteVariables(line, _variables);
            }

            // Use Regex to make sure we only match directives, not variables
            if(Regex.Match(line, @"\s*%[a-zA-z\-]").Success) {
                HandleDirective(line.Trim());
                line = "";
            }

            // If we are not processing text, ignore the line
            if(!_process.Peek()) {
                line = "";
            }

            if(line.Trim().Length > 0 && !line.Trim().StartsWith("#")) {
                // Line contains content to be processed
                // Strip any trailing comments from the line
                if (line.IndexOf(" #") > 0) {
                    line = line.Substring(0, line.IndexOf(" #")).TrimEnd();
                }
            }
            else {
                // Return a blank line
                line = "";
            }

            return line;
        }


        /// <summary>
        /// Disposes of each open file.
        /// </summary>
        public void Dispose()
        {
            while(_streams.Count > 0) {
                _streams.Pop().Dispose();
                _files.Pop();
                _lines.Pop();
            }
        }


        /// <summary>
        /// Handles a pre-processor directive (i.e. a line starting with %).
        /// </summary>
        protected void HandleDirective(string line)
        {
            // Pre-processor directive
            int pos = line.IndexOf(" ");
            string directive;
            if(pos >= 0) {
                directive = line.Substring(0, pos);
                line = line.Substring(pos).Trim();
            }
            else {
                directive = line;
                line = "";
            }
            _log.Debug("Handling pre-processor directive " + directive);

            if(_process.Peek() && (directive == @"%include")) {
                // Include specified file
                _log.Info("Including file '" + line + "'");
                IncludeFile(line);
            }
            else if(_process.Peek() && (directive == @"%set" || directive == @"%def" || directive == @"%set-if-undef")) {
                // Set value of substitution variable
                string key = line.Substring(0, line.IndexOf(" "));
                string val = line.Substring(line.IndexOf(" ")).Trim();
                if(directive != @"%set-if-undef" || !_variables.ContainsKey(key)) {
                    _variables[key] = val;
                }
            }
            else if(_process.Peek() && (directive == @"%unset" || directive == @"%undef")) {
                // Delete substitution variable
                _variables.Remove(line);
            }
            else if(directive == @"%if-def" || directive == @"%if-undef") {
                if(directive == @"%if-def") {
                    _process.Push(_process.Peek() && _variables.ContainsKey(line));
                }
                else {
                    _process.Push(_process.Peek() && !_variables.ContainsKey(line));
                }
                _log.Debug("Processing " + (_process.Peek() ? "enabled" : "disabled") + " for " + directive + " block");
            }
            else if(directive == @"%else") {
                if(_process.Count <= 1) {
                    throw new ParseException("Syntax error: %else without %if-def/%if-undef",
                        _files.Peek(), _lines.Peek());
                }
                _process.Push(!_process.Pop() && _process.Peek());
            }
            else if(directive == @"%end-if") {
                if(_process.Count <= 1) {
                    throw new ParseException("%end-if encountered without preceding %if-def/%if-undef",
                        _files.Peek(), _lines.Peek());
                }
                _log.Debug("End of %if-def/%if-undef block");
                _process.Pop();
            }
            else
            {
                if(_process.Peek()) {
                    throw new ParseException("Unknown preprocessor directive: " + directive,
                        _files.Peek(), _lines.Peek());
                }
            }
        }


        /// <summary>
        /// Includes the specified file, pushing the new file onto the stack, so
        /// that lines come from the new file until it is completed.
        /// </summary>
        protected void IncludeFile(string file)
        {
            // Ensure we don't have a cyclic dependency
            if(_files.Contains(file)) {
                throw new ParseException("Cyclic include dependency: " + file +
                    " has already been included", _files.Peek(), _lines.Peek());
            }

            // Open new stream
            _files.Push(file);
            _lines.Push(0);
            _streams.Push(new StreamReader(file));
        }


        /// <summary>
        /// Substitutes a value for any variables used in the supplied string.
        /// The variable values to be substituted are in the supplied dictionary.
        /// </summary>
        protected string SubstituteVariables(string line, Dictionary<string, object> variables)
        {
            // Perform any variable susbstitution on the line
            line = Regex.Replace(line, @"\%(\w+)\%", m => {
                string name = m.Groups[1].Captures[0].ToString();
                if(variables != null && variables.ContainsKey(name)) {
                    // Variable value was set on command-line, so use that
                    _log.Debug("Using substitution variable value for " + m.ToString());
                    return variables[name].ToString();
                }
                else if(Environment.GetEnvironmentVariables().Contains(name)) {
                    // Variable value has been set in environment variable, so use that
                    _log.Debug("Using environment variable value for " + m.ToString());
                    return Environment.GetEnvironmentVariable(name);
                }
                else if(Environment.GetEnvironmentVariables().Contains(name.ToUpper())) {
                    // Variable value has been set in environment variable, so use that
                    _log.Debug("Using environment variable value for " + m.ToString());
                    return Environment.GetEnvironmentVariable(name.ToUpper());
                }
                else {
                    // Variable not specified anywhere
                    throw new ParseException("No value was found for substitution variable '%" +
                        name + "%'", _files.Peek(), _lines.Peek());
                }
            });

            return line;
        }

    }

}
