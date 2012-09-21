using System;

using log4net;
using HSVSESSIONLib;
using HFMWSESSIONLib;
using HSVCALCULATELib;

using Command;


namespace HFM
{

    /// <summary>
    /// Represents a connection to a single HFM application. The main purpose of
    /// a Session is to obtain references to other functional modules for the
    /// current application.
    /// </summary>
    public class Session
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvSession object
        internal readonly HsvSession HsvSession;

        // Reference to the Calculate module
        private Calculate _calculate;


        public Session(HsvSession session)
        {
            HsvSession = session;
        }


        [Factory]
        public Calculate Calculate
        {
            get {
                if(_calculate == null) {
                    _calculate = new Calculate((HsvCalculate)HsvSession.Calculate);
                }
                return _calculate;
            }
        }

    }



    /// <summary>
    /// Holds a reference to an HFM web session. A web session is needed for
    /// working with documents; most other functionality is obtained through
    /// a Session object.
    /// </summary>
    public class WebSession
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvSession object
        internal readonly HFMwSession HFMwSession;


        public WebSession(HFMwSession session)
        {
            HFMwSession = session;
        }

    }

}
