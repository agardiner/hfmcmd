using System;
using System.Runtime.InteropServices;

using log4net;
using Utilities;

#if !LATE_BIND
using HSVRESOURCEMANAGERLib;
#endif
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

        // Constants for specific versions of HFM
        public static Version VER_9_3_1 = "9.3.1".ToVersion();
        public static Version VER_11_1 = "11.1".ToVersion();
        public static Version VER_11_1_2_1 = "11.1.2.1".ToVersion();
        public static Version VER_11_1_2_2 = "11.1.2.2".ToVersion();
        public static Version VER_11_1_2_2_300 = "11.1.2.2.300".ToVersion();

        public static Version VER_LAST_TESTED = VER_11_1_2_2_300;

        /// Returns the version against which HFMCmd was built
        public static Version BuildVersion
        {
            get {
#if HFM_11_1_2_2_300
                return VER_11_1_2_2_300;
#elif HFM_11_1_2_2
                return VER_11_1_2_2;
#elif HFM_11_1_2_1
                return VER_11_1_2_1;
#elif HFM_9_3_1
                return VER_9_3_1;
#endif
            }
        }

        /// Returns the current installed HFM version
        public static Version Version { get { return ResourceManager.Version; } }
        public static string VersionString { get { return ResourceManager.VersionString; } }
        /// Returns true if the current HFM instance supports variable number of customs
        public static bool HasVariableCustoms
        {
            get {
                return Version >= VER_11_1_2_2;
            }
        }


        /// <summary>
        /// Check that HFMCmd has been built against the right library for this
        /// version of HFM.
        /// </summary>
        public static void CheckVersionCompatibility()
        {
#if !LATE_BIND
#if HFM_11_1_2_2_300
            if(Version < VER_11_1_2_2_300) {
#elif HFM_11_1_2_2
            if(Version < VER_11_1_2_2 || Version >= VER_11_1_2_2_300) {
#elif HFM_11_1_2_1
            if(Version < VER_11_1 || Version > VER_11_1_2_1) {
#elif HFM_9_3_1
            if(Version < VER_9_3_1 || Version >= VER_11_1) {
#else
#error No HFM_<version> #define found for early-bind build! When compiling, either LATE_BIND or HFM_<version> must be set.
            // This line is here to stop the compiler complaining about syntax
            // errors in this file due to the missing if statement. If we get
            // here, it means no HFM__<VERSION> #define was specified at build
            if(true) {
#endif
                ThrowIncompatibleLibraryEx();
            }
            else if(Version > VER_LAST_TESTED) {
                _log.WarnFormat("Your version of HFM ({0}) may not be compatible " +
                        "with the version of HFM for which HFMCmd was built ({1})",
                        Version, BuildVersion);
            }
#endif
        }


#if LATE_BIND
        /// <summary>
        /// Method for instantiating a late-bound instance of an OLE automation
        /// object by its program id.
        /// </summary>
        public static dynamic CreateObject(string hfmProgId)
        {
            try {
                Type type = Type.GetTypeFromProgID(hfmProgId, true);
                return Activator.CreateInstance(type);
            }
            catch(COMException ex) {
                unchecked {
                    if(ex.ErrorCode == (int)0x80040154 || ex.ErrorCode == (int)0x800401F3) {
                        _log.Error(string.Format("Unable to instantiate a {0} COM object; " +
                                   "is HFM installed on this machine?", hfmProgId), ex);
                    }
                }
                throw ex;
            }
        }
#endif

        public static void ThrowIncompatibleLibraryEx()
        {
            throw new Exception("The installed version of HFM on this machine supports features " +
                                "that were not available in the library with which HFMCmd was " +
                                "compiled. Please download the correct version of HFMCmd for " +
                                "HFM version " + VersionString);
        }


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
            string action = null;
            if(_log.IsTraceEnabled()) {
                action = string.Format(format, arg);
            }
            Try(action, op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, object arg1, Action op)
        {
            string action = null;
            if(_log.IsTraceEnabled()) {
                action = string.Format(format, arg0, arg1);
            }
            Try(action, op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, object arg1, object arg2, Action op)
        {
            string action = null;
            if(_log.IsTraceEnabled()) {
                action = string.Format(format, arg0, arg1, arg2);
            }
            Try(action, op);
        }


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(string format, object arg0, object arg1, object arg2, object arg3, Action op)
        {
            string action = null;
            if(_log.IsTraceEnabled()) {
                action = string.Format(format, arg0, arg1, arg2, arg3);
            }
            Try(action, op);
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
            if(action != null) {
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
