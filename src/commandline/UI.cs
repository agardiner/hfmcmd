using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Enumeration of interrupt events that might be received by the process
    /// via a ConsoleCtrlHandler callback.
    /// </summary>
    public enum EInterruptTypes
    {
        Ctrl_C = 0,
        Ctrl_Break = 1,
        Close = 2,
        Logoff = 5,
        Shutdown = 6
    }



    /// <summary>
    /// Class to hold definition of external SetConsoleCtrlHandler routine.
    /// </summary>
    public class Win32
    {
        public delegate bool Handler(EInterruptTypes ctrlType);

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(Handler handler, bool Add);
    }



    /// <summary>
    /// Main class for interacting via the command-line. Handles the definition
    /// and parsing of command-line arguments, and the display of usage and help
    /// messages.
    /// </summary>
    public class UI
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Static public flag indicating that the application is to terminate
        /// immediately, e.g. in response to a Ctrl-C or Logoff event. Any long-
        /// running command should check this flag periodically and attempt to
        /// abort gracefully.
        /// </summary>
        public static bool Interrupted = false;


        /// <summary>
        /// This method registers this class as a handler for Ctrl-C etc events
        /// in the console. It returns a handle to the handler, which should be
        /// referenced via the following at the end of the program Main method:
        ///     GC.KeepAlive(hr);
        /// </summary>
        public static Win32.Handler RegisterCtrlHandler()
        {
            // Hook up CtrlHandler to handle breaks, logoffs, etc
            Win32.Handler hr = new Win32.Handler(UI.CtrlHandler);
            Win32.SetConsoleCtrlHandler(hr, true);
            return hr;
        }


        /// <summary>
        /// Handler to receive control events, such as Ctrl-C and logoff and
        /// shutdown events. As a minimum, this logs the event, so that a record
        /// of why the process exited is maintained.
        /// </summary>
        /// <param name="ctrlType">The type of event that occurred.</param>
        /// <returns>True, indicating we have handled the event.</returns>
        static bool CtrlHandler(EInterruptTypes ctrlType)
        {
            _log.Warn("An interrupt [" + ctrlType + "] has been received");
            Interrupted = true;

            return true;
        }


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
        public UI(string purpose)
        {
            Definition = new Definition { Purpose = purpose };
        }


        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc)
        {
            return AddPositionalArgument(key, desc, null);
        }

        /// <summary>
        /// Convenience method for defining a new positional argument.
        /// </summary>
        public PositionalArgument AddPositionalArgument(string key, string desc,
                Argument.OnParseHandler onParse)
        {
            var arg = new PositionalArgument { Key = key, Description = desc };
            arg.OnParse += onParse;
            return (PositionalArgument)Definition.AddArgument(arg);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc)
        {
            return AddKeywordArgument(key, desc, null);
        }

        /// <summary>
        /// Convenience method for defining a new keyword argument.
        /// </summary>
        public KeywordArgument AddKeywordArgument(string key, string desc,
                Argument.OnParseHandler onParse)
        {
            var arg = new KeywordArgument { Key = key, Description = desc };
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
            var arg = new FlagArgument { Key = key, Description = desc };
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
                Definition.DisplayUsage(args[0], System.Console.Error, result);
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
        /// Writes a blank line to the console.
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
