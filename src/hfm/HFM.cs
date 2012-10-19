using System;
using System.Runtime.InteropServices;

using log4net;

using HSVRESOURCEMANAGERLib;
using HFMCONSTANTSLib;


namespace HFM
{

    /// <summary>
    /// Contains static methods for wrapping blocks of code that interact with
    /// HFM via the COM API. As any API call may throw a COMException, the Try
    /// methods in this class can be used to wrap these calls, and recover more
    /// details from HFM about the nature and cause of the error. In particular,
    /// the COMException contains only an error code, but the HFMResourceManager
    /// can be queried to get a description of the error from the code.
    /// </summary>
    public class HFM
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Returns the current installed HFM version
        public static string Version { get { return ResourceManager.Version; } }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(Action op)
        {
            Try(null, op, ex => { throw ex; });
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg, Action op)
        {
            Try(string.Format(format, arg), op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, string arg1, Action op)
        {
            Try(string.Format(format, arg0, arg1), op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, string arg1, string arg2, Action op)
        {
            Try(string.Format(format, arg0, arg1, arg2), op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, string arg1, string arg2, string arg3, Action op)
        {
            Try(string.Format(format, arg0, arg1, arg2, arg3), op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against
        /// HFM, prefixing the API call with a log message.
        /// </summary>
        /// <param name="action">A message describing the action being taken;
        /// this will be logged before the operation is performed.</param>
        /// <param name="op">An action delegate that contains the HFM API calls.
        /// </param>
        public static void Try(string action, Action op)
        {
            Try(action, op, ex => { throw ex; });
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// As any HFM API call may throw a COMException, we catch these and
        /// convert them to HFMExceptions, which retrieve the associated error
        /// error. The HFMException is then passed to the specified exception
        /// handler routine, which can take any clean-up action needed before
        /// (re-)throwing the exception.
        /// </summary>
        /// <param name="action">A message describing the action to be taken;
        /// this will be logged before the operation is performed.</param>
        /// </param>
        /// <param name="op">An action delegate that contains the HFM API calls.
        /// </param>
        /// <param name="hndlr">A delegate that will handle the HFM exception.
        /// </param>
        public static void Try(string action, Action op, Action<HFMException> handler)
        {
            if (action != null) {
                _log.Trace(action);
            }
            try {
                op();
            }
            catch (COMException ex) {
                handler(new HFMException(ex));
            }
            catch (HFMException ex) {
                handler(ex);
            }
        }


        /// <summary>
        /// Convenience method for dealing with the annoying fact that arrays
        /// returned from HFM have type Object, and need to be cast to the
        /// appropriate native array type.
        /// </summary>
        public static T[] Object2Array<T>(object retVal)
        {
            return retVal != null ? Array.ConvertAll((object[])retVal, o => (T)o) : (T[])retVal;
        }

    }

}
