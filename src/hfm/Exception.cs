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

}
