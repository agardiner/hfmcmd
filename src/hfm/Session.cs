using System;

using log4net;
using HSVSESSIONLib;


namespace HFM
{
    public class Session
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvSession object
        protected readonly HsvSession _session;


        public Session(HsvSession session)
        {
            _session = session;
        }
    }
}
