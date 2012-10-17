using System;
using System.Collections.Generic;

using log4net;

using HSVSESSIONLib;
using HSVCALCULATELib;
using HFMCONSTANTSLib;

using Command;
using HFMCmd;


namespace HFM
{

    public enum EConsolidationType : short
    {
        All = tagCONSOLIDATIONTYPE.CONSOLIDATE_ALL,
        AllWithData = tagCONSOLIDATIONTYPE.CONSOLIDATE_ALLWITHDATA,
        EntityOnly = tagCONSOLIDATIONTYPE.CONSOLIDATE_ENTITYONLY,
        ForceEntityOnly = tagCONSOLIDATIONTYPE.CONSOLIDATE_FORCEENTITYONLY,
        Impacted = tagCONSOLIDATIONTYPE.CONSOLIDATE_IMPACTED
    }


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

        internal HsvCalculate HsvCalculate { get { return _hsvCalculate; } }



        [Factory]
        public Calculate(Session session)
        {
            _hsvCalculate = (HsvCalculate)session.HsvSession.Calculate;
        }


        [Command("Performs an allocation")]
        public void Allocate(
                Metadata metadata,
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
                string[] entities,
                [Parameter("The value member(s) for which the allocation should be peformed",
                           Alias = "Value")]
                string[] values)
        {
            var scenId = metadata["Scenario"].GetId(scenario);
            var yearId = metadata["Year"].GetId(year);
            var startPeriodId = metadata["Period"].GetId(startPeriod);
            var entityMembers = metadata["Entity"].GetMembers(entities);
            var valueMembers = metadata["Value"].GetMembers(values);

            foreach(var entityMbr in entityMembers) {
                var entity = entityMbr as Entity;
                foreach(var value in valueMembers) {
                    if(endPeriod == null) {
                        HFM.Try("Allocating",
                                () => HsvCalculate.Allocate(scenId, yearId, startPeriodId,
                                                            entity.Id, entity.ParentId, value.Id));
                    }
                    else {
                        var endPeriodId = metadata["Period"].GetId(endPeriod);
                        HFM.Try("Allocating",
                                () => HsvCalculate.Allocate2(scenId, yearId, startPeriodId, endPeriodId,
                                                             entity.Id, entity.ParentId, value.Id));
                    }
                }
            }
        }


        [Command("Performs a calculation")]
        public void ChartLogic(
                Metadata metadata,
                [Parameter("The scenario in which to perform the calculation")]
                string scenario,
                [Parameter("The year for which to perform the calculation")]
                string year,
                [Parameter("The first period over which the calculation should be performed",
                           Alias = "Period")]
                string startPeriod,
                [Parameter("The last period over which the calculation should be performed",
                           DefaultValue = null)]
                string endPeriod,
                [Parameter("The entities for which the calculation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) for which the calculation should be peformed",
                           Alias = "Value")]
                string[] values,
                [Parameter("Flag indicating whether to force a calculation when not needed",
                           DefaultValue = false)]
                bool force)
        {
            var scenId = metadata["Scenario"].GetId(scenario);
            var yearId = metadata["Year"].GetId(year);
            var startPeriodId = metadata["Period"].GetId(startPeriod);
            var entityMembers = metadata["Entity"].GetMembers(entities);
            var valueMembers = metadata["Value"].GetMembers(values);

            foreach(var entityMbr in entityMembers) {
                var entity = entityMbr as Entity;
                foreach(var value in valueMembers) {
                    if(endPeriod == null) {
                        HFM.Try("Calculating",
                                () => HsvCalculate.ChartLogic(scenId, yearId, startPeriodId,
                                                              entity.Id, entity.ParentId, value.Id, force));
                    }
                    else {
                        var endPeriodId = metadata["Period"].GetId(endPeriod);
                        HFM.Try("Calculating",
                                () => HsvCalculate.ChartLogic2(scenId, yearId, startPeriodId, endPeriodId,
                                                               entity.Id, entity.ParentId, value.Id, force));
                    }
                }
            }
        }


        [Command("Performs a consolidation")]
        public void Consolidate(
                Metadata metadata,
                [Parameter("The scenario in which to perform the consolidation")]
                string scenario,
                [Parameter("The year for which to perform the consolidation")]
                string year,
                [Parameter("The first period over which the consolidation should be performed",
                           Alias = "Period")]
                string startPeriod,
                [Parameter("The last period over which the consolidation should be performed",
                           DefaultValue = null)]
                string endPeriod,
                [Parameter("The entities for which the consolidation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The type of consolidation to perform",
                           DefaultValue = EConsolidationType.Impacted)]
                EConsolidationType consolidationType,
                SystemInfo si,
                IOutput output)
        {
            var scenId = metadata["Scenario"].GetId(scenario);
            var yearId = metadata["Year"].GetId(year);
            var startPeriodId = metadata["Period"].GetId(startPeriod);
            var entityMembers = metadata["Entity"].GetMembers(entities);

            foreach(var entityMbr in entityMembers) {
                var entity = entityMbr as Entity;
                si.MonitorBlockingTask(output);
                if(endPeriod == null) {
                    HFM.Try("Consolidating",
                            () => HsvCalculate.Consolidate(scenId, yearId, startPeriodId,
                                                           entity.Id, entity.ParentId,
                                                           (short)consolidationType));
                }
                else {
                    var endPeriodId = metadata["Period"].GetId(endPeriod);
                    HFM.Try("Consolidating",
                            () => HsvCalculate.Consolidate2(scenId, yearId, startPeriodId, endPeriodId,
                                                            entity.Id, entity.ParentId,
                                                            (short)consolidationType));
                }
                si.BlockingTaskComplete();
            }
        }
    }
}
