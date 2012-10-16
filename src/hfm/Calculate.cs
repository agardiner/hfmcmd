using System;
using System.Collections.Generic;

using log4net;
using HSVSESSIONLib;
using HSVCALCULATELib;

using Command;


namespace HFM
{

    /// <summary>
    /// Wraps the HsvCalculate module, exposing its functionality for performing
    /// calculations, allocations, translations, consolidations etc.
    /// </summary>
    public class Calculate
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvCalculate object
        protected readonly HsvCalculate _hsvCalculate;


        [Factory]
        public Calculate(HsvSession session)
        {
            _hsvCalculate = (HsvCalculate)session.Calculate;
        }


        [Command("Performs an allocation over multiple periods")]
        public void Allocate(
                [Parameter("The scenario in which to perform the allocation")]
                string scenario,
                [Parameter("The year for which to perform the allocation")]
                string year,
                [Parameter("The first period over which the allocation should be performed",
                           Alias = "Period")]
                string startPeriod,
                [Parameter("The last period over which the allocation should be performed",
                           DefaultValue = null)]
                string endPeriod,
                [Parameter("The entities for which the allocation should be performed",
                           Alias = "Entity")]
                List<string> entities,
                [Parameter("The value member(s) for which the allocation should be peformed",
                           Alias = "Value")]
                List<string> values)
        {

        }
    }
}
