using System;
using System.Runtime.InteropServices;

using log4net;

using HSVRESOURCEMANAGERLib;
using HFMCONSTANTSLib;


namespace HFM
{

    /// <summary>
    /// Exception class for errors thrown by HFM.
    /// </summary>
    [Serializable]
    public class HFMException : ApplicationException
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Holds the formatted error message returned from HFM
        protected readonly String _formattedMessage;

        // Holds the error code returned by HFM
        protected readonly int _errorCode;


        /// <summary>
        /// Overrides the Message property to return the formatted exception
        /// message returned from HFM.
        /// </summary>
        public override string Message { get { return _formattedMessage; } }


        /// <summary>
        /// Returns the error code returned by HFM.
        /// </summary>
        public int ErrorCode { get { return _errorCode; } }


        /// <summary>
        /// Constructor for creating an HFM exception from a COMException.
        /// </summary>
        /// <param name="inner">The COMException that is to be converted
        /// to an HFMException.</param>
        public HFMException(COMException inner)
            : base("An exception occurred while interacting with HFM", inner)
        {
            _formattedMessage = ResourceManager.GetErrorMessage(inner.ErrorCode, inner.Message);
            LogException();
        }


        /// <summary>
        /// Create an HFMException for a given error code.
        /// </summary>
        public HFMException(int errorCode)
        {
            _formattedMessage = ResourceManager.GetErrorMessage(errorCode, "Unknown Error");
            LogException();
        }


        /// <summary>
        /// Create an HFMException with the specified error message.
        /// </summary>
        public HFMException(string errorMsg)
        {
            _formattedMessage = errorMsg;
            LogException();
        }


        /// <summary>
        /// Constructor needed for serialization when exception propagates from a
        /// remoting server to the client.
        /// </summary>
        protected HFMException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }


        /// Logs the exception details for debug purposes.
        protected void LogException()
        {
            if (_log.IsDebugEnabled) {
                _log.Error("An exception occurred while interacting with HFM:");
                _log.Error(_formattedMessage, this);
            }
        }
    }



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


        /// <summary>
        /// Encapsulate a common pattern for performing an API call against HFM.
        /// </summary>
        public static void Try(Action op)
        {
            Try(null, op, ex => { throw ex; });
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
                _log.Info(action);
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

    }

}
