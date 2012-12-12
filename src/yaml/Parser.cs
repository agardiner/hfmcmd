using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using log4net;


namespace YAML
{

    /// <summary>
    /// Exception class for YAML parse errors.
    /// </summary>
    public class ParseException : Exception
    {
        /// The source YAML document filename
        public readonly string FileName;
        /// The line number in the source YAML document at which the parse
        /// exception occurred
        public readonly int Line;


        /// <summary>
        /// Constructs an instance of a ParseException
        /// </summary>
        public ParseException(string msg, string fileName, int line)
            : base(msg)
        {
            FileName = fileName;
            Line = line;
        }


        /// <summary>
        /// Constructs an instance of a ParseException wrapping an inner
        /// exception.
        /// </summary>
        public ParseException(string msg, Exception inner, string fileName, int line)
            : base(msg, inner)
        {
            FileName = fileName;
            Line = line;
        }


        /// <summary>
        /// Overrides Message property to return the parse error message and
        /// the line number on which it occurred.
        /// </summary>
        public override string Message
        {
            get {
                return string.Format("Parse error in {0} at line {1}: {2}",
                                     FileName, Line, base.Message);
            }
        }
    }


    /// <summary>
    /// Class for parsing YAML files.
    /// </summary>
    public class Parser
    {
        // Structure holding collection Nodes and their indentation levels
        protected struct NodeLevel
        {
            public Node Node;
            public int  Indentation;
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to the pre-processor feeding us lines of YAML
        protected Preprocessor _preprocessor;

        // A stack representing the currently nested Nodes and their indentation
        // levels
        protected Stack<NodeLevel> _stack = new Stack<NodeLevel>();


        /// <summary>
        /// Attempts to parse the YAML file contents of templateFile into a YAML
        /// Node object.
        /// </summary>
        /// <returns>A YAML Node object to the root node if parsing is
        /// successful.
        /// </returns>
        public Node ParseFile(string fileName, Dictionary<string, object> variables)
        {
            // Create root node
            _stack.Clear();
            NodeLevel context = new NodeLevel() { Node = new Node(), Indentation = -2 };
            _stack.Push(context);

            using(_preprocessor = new Preprocessor(fileName, variables))
            {
                string line;

                // Process each line of the file
                do {
                    line = _preprocessor.ReadLine();
                    if(line == null) { continue; }

                    if(line.Trim().Length > 0 && !line.Trim().StartsWith("#")) {
                        // First line determines type of parent collection
                        try {
                            ProcessLine(line);
                        }
                        catch(ParseException) {
                            throw;
                        }
                        catch(Exception ex) {
                            throw new ParseException("An exception occurred",
                                ex, _preprocessor.File, _preprocessor.Line);
                        }
                    }
                }
                while(line != null);
            }

            // Get root object to return; this is at the bottom of the stack
            do {
                context = _stack.Pop();
            }
            while (_stack.Count > 0);

            return context.Node;
        }


        /// <summary>
        /// Process a single line of text into the Node that is the current
        /// context.
        /// </summary>
        protected void ProcessLine(string line)
        {
            // Determine if line is at same level of indentation as previous
            NodeLevel context = _stack.Peek();
            Node node;
            string indent = line;
            line = line.TrimStart(null);
            indent = indent.Substring(0, indent.Length - line.Length);
            int indentation = indent.Length;

            // Ensure indentation is via spaces and not tabs
            if(indent.IndexOf('\t') >= 0) {
                throw new ParseException("Tab characters must not be used for indentation (use spaces instead)",
                    _preprocessor.File, _preprocessor.Line);
            }

            if(indentation < context.Indentation) {
                // Finished populating current nesting
                while (indentation < context.Indentation) {
                    _stack.Pop();
                    context = _stack.Peek();
                }
                if(indentation != context.Indentation) {
                    throw new ParseException("Unexpected indentation level (got " + indentation +
                        ", expected " + context.Indentation + ")", _preprocessor.File, _preprocessor.Line);
                }
            }
            if(indentation == context.Indentation) {
                _stack.Pop();
                context = _stack.Peek();
            }

            if(line.StartsWith("-")) {
                node = ProcessListElement(context.Node, line);
            }
            else if(line.IndexOf(":") > 0) {
                node = ProcessDictionaryElement(context.Node, line);
            }
            else {
                // NOTE: Non-standard YAML, but simplifies control file specification
                // Basically, if we find a line that is simply a string on its own,
                // assume it is a list element and process it as such.
                node = ProcessListElement(context.Node, "- " + line);
            }
            _stack.Push(new NodeLevel() { Node = node, Indentation = indentation });
        }


        /// <summary>
        /// Processes the current line into an existing list.
        /// </summary>
        /// <param name="context">Node containing the list to which the
        /// element should be added.</param>
        /// <param name="line">The line containing the list element (complete
        /// with leading '-').</param>
        protected Node ProcessListElement(Node context, string line)
        {
            var value = ParseValue(null, line.Substring(1).Trim());
            return context.Add(value);
        }


        /// <summary>
        /// Processes the current line into an existing Dictionary.
        /// </summary>
        /// <param name="context">Node containing the Dictionary to which the
        /// element should be added.</param>
        /// <param name="line">The line containing the key: value pair.</param>
        protected Node ProcessDictionaryElement(Node context, string line)
        {
            string key = line.Substring(0, line.IndexOf(":")).Trim();
            line = line.Substring(line.IndexOf(":") + 1).Trim();
            var node = ParseValue(key, line);
            return context.Add(node);
        }


        /// <summary>
        /// Converts a string value to a string, int or boolean type.
        /// </summary>
        /// <param name="value">The string that is to be parsed.</param>
        /// <returns>An object that is the parsed value of the string.</returns>
        protected Node ParseValue(string key, string value)
        {
            if(value.ToUpper() == "TRUE") {
                return new Node(key, true);
            }
            else if(value.ToUpper() == "FALSE") {
                return new Node(key, false);
            }
            else if(value == "") {
                return new Node(key, null);
            }
            else if(Regex.Match(value, @"\A([""'])(.*)\1\Z").Success) {
                return new Node(key, value.Substring(1, value.Length - 2));
            }
            else if(Regex.Match(value, @"\A\[?(.+)(,(.+))+\]?\Z").Success) {
                // Value is an array of items
                if(value.StartsWith("[") && value.EndsWith("]")) {
                    value = value.Substring(1, value.Length - 2);
                }
                string[] vals = value.Split(',');
                Node coll = new Node(key, null);
                foreach(string val in vals) {
                    coll.Add(ParseValue(null, val.Trim()));
                }
                return coll;
            }
            else if(Regex.Match(value, @"\{(.+:.+)(?:,(.+:.+))*\}").Success) {
                // Value is an dictionary of items
                value = value.Substring(1, value.Length - 2);
                string[] vals = value.Split(',');
                Node coll = new Node(key, null);
                foreach(string val in vals) {
                    string subkey = val.Substring(0, val.IndexOf(':'));
                    coll.Add(ParseValue(subkey, val.Substring(val.IndexOf(':') + 1)));
                }
                return coll;
            }
            else {
                return new Node(key, value);
            }
        }

    }

}
