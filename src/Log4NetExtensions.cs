using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using log4net.ObjectRenderer;
using log4net.Repository.Hierarchy;



namespace log4net
{

    /// <summary>
    /// Extension methods for adding additional log shortcuts for Verbose and
    /// Trace level logging.
    /// </summary>
    public static class ILogExtentions
    {

        public static bool IsFineEnabled(this ILog log)
        {
            return log.Logger.IsEnabledFor(log4net.Core.Level.Fine);
        }

        public static void Fine(this ILog log, string message, Exception exception)
        {
            log.Logger.Log(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Fine, message, exception);
        }

        public static void Fine(this ILog log, string message)
        {
            log.Fine(message, null);
        }

        public static void FineFormat(this ILog log, string message, params object[] args)
        {
            if(log.Logger.IsEnabledFor(log4net.Core.Level.Fine)) {
                log.Fine(string.Format(message, args), null);
            }
        }


        public static bool IsTraceEnabled(this ILog log)
        {
            return log.Logger.IsEnabledFor(log4net.Core.Level.Trace);
        }

        public static void Trace(this ILog log, string message, Exception exception)
        {
            log.Logger.Log(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Trace, message, exception);
        }

        public static void Trace(this ILog log, string message)
        {
            log.Trace(message, null);
        }

        public static void TraceFormat(this ILog log, string message, params object[] args)
        {
            if(log.Logger.IsEnabledFor(log4net.Core.Level.Trace)) {
                log.Trace(string.Format(message, args), null);
            }
        }

    }


    /// <summary>
    /// Class for handling logging of exceptions. Used to avoid the default
    /// behaviour of logging a full back-trace when an exception occurs.
    /// </summary>
    public sealed class ExceptionMessageRenderer : IObjectRenderer
    {

        /// Reference to the logger hierarchy
        private Hierarchy _logHierarchy;

        /// Constructor
        public ExceptionMessageRenderer()
        {
            _logHierarchy = LogManager.GetRepository() as Hierarchy;
        }


        /// Implementation of the IObjectRenderer interface. Called on when an
        /// exception is logged.
        public void RenderObject(RendererMap map, object obj, System.IO.TextWriter writer)
        {
            Exception ex = obj as Exception;

            for (; ex != null; ex = ex.InnerException) {
                if (ex is COMException && ex.Message.StartsWith("<?xml")) {
                    writer.WriteLine();
                    writer.Write("HFMException message XML contents:");
                    writer.Write(YAML.XML.ConvertXML(ex.Message));
                }
                else {
                    writer.Write(ex.GetType().Name);
                    writer.Write(": ");
                    writer.Write(ex.Message);
                }
                if (ex.InnerException != null) {
                    writer.WriteLine();
                }
                else if (_logHierarchy.Root.Level.CompareTo(log4net.Core.Level.Fine) < 0) {
                    writer.WriteLine();
                    writer.WriteLine("Backtrace:");
                    writer.Write(ex.StackTrace);
                }
            }

        }
    }

}
