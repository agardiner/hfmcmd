using System;

using log4net;
using HSVCALCULATELib;

using Command;


namespace HFM
{
    public class Calculate
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvCalculate object
        protected readonly HsvCalculate _calculate;


        protected internal Calculate(HsvCalculate calc)
        {
            _calculate = calc;
        }


        [Command("Performs an allocation")]
        public void Allocate()
        {

        }
    }
}
