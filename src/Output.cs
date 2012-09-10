using System;

using log4net;



namespace Command
{

    public interface IOutput
    {
        void SetFields(params string[] fields);
        void WriteRecord(params string[] values);
    }


    public class LogOutput : IOutput
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        public void SetFields(params string[] fields)
        {
            _log.Info(String.Join(" ", fields));
        }

        public void WriteRecord(params string[] values)
        {
            _log.Info(String.Join(" ", values));
        }
    }

}
