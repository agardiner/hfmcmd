using System;

using log4net;
using HSVSESSIONLib;
using HSVCALCULATELib;

using Command;


namespace HFM
{
    public class Session
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvSession object
        protected readonly HsvSession _session;

        private Calculate _calculate;


        public Session(HsvSession session)
        {
            _session = session;
        }


        [Factory]
        public Calculate Calculate
        {
            get {
                if(_calculate == null) {
                    _calculate = new Calculate((HsvCalculate)_session.Calculate);
                }
                return _calculate;
            }
        }

    }
}
