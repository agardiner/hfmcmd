using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Main class for interacting via the command-line. Handles the definition
    /// and parsing of command-line arguments, and the display of usage and help
    /// messages.
    /// </summary>
    public class UI
    {
        /// Flag indicating whether console output is redirected
        private bool _isRedirected = false;

        /// The set of possible arguments to be recognised.
        public Definition Definition;

        /// Returns the console width, or -1 if the console is redirected
        public int ConsoleWidth {
            get {
                if(_isRedirected) { return -1; }
                try {
                    return System.Console.WindowWidth;
                }
                catch(IOException) {
                    _isRedirected = true;
                    return -1;
                }
            }
        }


        /// <summary>
        /// Constructor; requires a purpose for the program whose args we are
        /// parsing.
        /// </summary>
        public UI(string purpose) : this(purpose, null) {}


        /// <summary>
        /// Constructor; requires a purpose for the program whose args we are
        /// parsing.
        /// </summary>
        public UI(string purpose, IArgumentMapper argMap)
        {
            Definition = new Definition { Purpose = purpose, ArgumentMapper = argMap };
        }


        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc)
        {
            return AddPositionalArgument(key, desc, typeof(string), null);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Type type)
        {
            return AddPositionalArgument(key, desc, type, null);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Argument.OnParseHandler onParse)
        {
            return AddPositionalArgument(key, desc, typeof(string), onParse);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc, Type type,
                Argument.OnParseHandler onParse)
        {
            var arg = new PositionalArgument { Key = key, Description = desc, Type = type };
            arg.OnParse += onParse;
            return (PositionalArgument)Definition.AddArgument(arg);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc)
        {
            return AddKeywordArgument(key, desc, typeof(string), null);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Type type)
        {
            return AddKeywordArgument(key, desc, type, null);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Argument.OnParseHandler onParse)
        {
            return AddKeywordArgument(key, desc, typeof(string), onParse);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc, Type type,
                Argument.OnParseHandler onParse)
        {
            var arg = new KeywordArgument { Key = key, Description = desc, Type = type };
            arg.OnParse += onParse;
            return (KeywordArgument)Definition.AddArgument(arg);
        }

        /// <summary>
        /// Convenience method for defining a new flag argument.
        /// </summary>
        public FlagArgument AddFlagArgument(string key, string desc)
        {
            return AddFlagArgument(key, desc, null);
        }


        /// <summary>
        /// Convenience method for defining a new flag argument.
        /// </summary>
        public FlagArgument AddFlagArgument(string key, string desc,
                Argument.OnParseHandler onParse)
        {
            var arg = new FlagArgument { Key = key, Description = desc, Type = typeof(bool) };
            arg.OnParse += onParse;
            return (FlagArgument)Definition.AddArgument(arg);
        }


        /// <summary>
        /// Parses the supplied set of arg strings using the list of Argument
        // definitions maintained by this command-line UI instance.
        /// </summary>
        public Dictionary<string, object> Parse(string[] args)
        {
            var parser = new Parser(Definition);
            var result = parser.Parse(new List<string>(args));
            if(parser.ShowUsage) {
                Definition.DisplayUsage(args[0], System.Console.Error);
                result = null;  // Don't act on what we parsed
            }
            else if(parser.ParseException != null) {
                throw parser.ParseException;
            }
            return result;
        }


        /// <summary>
        /// Writes a line of text to the console, ensuring lines the same width
        /// as the console don't output an unnecessary new-line.
        /// </summary>
        public void WriteLine(string line)
        {
            if(line.Length == ConsoleWidth) {
                System.Console.Out.Write(line);
            }
            else {
                System.Console.Out.WriteLine(line);
            }
        }


        /// <summary>
        /// Writes a line of text to the console, ensuring lines the same width
        /// as the console don't output an unnecessary new-line.
        /// </summary>
        public void WriteLine()
        {
            System.Console.Out.WriteLine();
        }


        /// <summary>
        /// Writes a partial line of text to the console, without moving to the
        /// next line.
        /// </summary>
        public void Write(string line)
        {
            System.Console.Out.Write(line);
        }


        /// <summary>
        /// Clears the current line of the console.
        /// </summary>
        public void ClearLine()
        {
            if(ConsoleWidth > -1 && System.Console.CursorLeft > 0) {
                var buf = new char[ConsoleWidth - 1];
                System.Console.CursorLeft = 0;
                System.Console.Write(buf);
                System.Console.CursorLeft = 0;
            }
        }


        /// <summary>
        /// Returns true if escape has been pressed.
        /// </summary>
        public bool EscPressed()
        {
            bool esc = false;

            if (System.Console.KeyAvailable) {
                var keyInfo = System.Console.ReadKey();
                if(keyInfo.Key == System.ConsoleKey.Escape) {
                    esc = true;
                }
            }
            return esc;
        }

    }

}
