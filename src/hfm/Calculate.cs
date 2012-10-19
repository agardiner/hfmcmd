using System;
using System.Collections.Generic;
using System.Linq;

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

        protected delegate void CalcOp(Member scenario, Member year, Member period,
                                       Member entity, Member value);


        [Factory]
        public Calculate(Session session)
        {
            _hsvCalculate = (HsvCalculate)session.HsvSession.Calculate;
        }


        /// Generic method for performing a calculation operation over a series
        /// of scenario, year, period, entity, and value members.
        protected void DoCalcOp(string op, Metadata metadata, string[] scenarios,
                string[] years, string[] periods, string[] entities, string[] values,
                IOutput output, CalcOp calcOp)
        {
            var scenMembers = metadata["Scenario"].GetMembers(scenarios);
            var yearMembers = metadata["Year"].GetMembers(years);
            var periodMembers = metadata["Period"].GetMembers(periods);
            var entityMembers = metadata["Entity"].GetMembers(entities);
            var valueMembers = metadata["Value"].GetMembers(values);

            // Cross-join the selections for scenario, year, entity, and value
            var loop =
              from s in scenMembers
              from y in yearMembers
              from e in entityMembers
              from v in valueMembers
              from p in periodMembers
              select new { Scenario = s, Year = y, Period = p, Entity = e, Value = v };

            // Calculate number of iterations, and measure progress
            var members = loop.ToArray();

            output.InitProgress(op, members.Length);
            var complete = 0;
            foreach(var i in members) {
                _log.FineFormat("{0} Scenario: {1}, Year: {2}, Period: {3}, Entity: {4}, Value: {5}",
                                op, i.Scenario.Name, i.Year.Name, i.Period.Name, i.Entity.Name, i.Value.Name);
                calcOp(i.Scenario, i.Year, i.Period, i.Entity, i.Value);
                if(output.SetProgress(++complete)) {
                    break;
                }
            }
            output.EndProgress();
        }


        [Command("Performs an allocation")]
        public void Allocate(
                Metadata metadata,
                [Parameter("The scenario(s) in which to perform the allocation",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) for which to perform the allocation",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) over which the allocation should be performed",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entity member(s) for which the allocation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) for which the allocation should be peformed",
                           Alias = "Value")]
                string[] values,
                IOutput output)
        {
            DoCalcOp("Allocating", metadata, scenarios, years, periods, entities, values, output,
                     (scenario, year, period, entity, value) =>
                {
                    HFM.Try("Allocating",
                            () => HsvCalculate.Allocate(scenario.Id, year.Id, period.Id,
                                                        entity.Id, entity.ParentId, value.Id));
                }
            );
        }


        [Command("Performs a calculation")]
        public void ChartLogic(
                Metadata metadata,
                [Parameter("The scenario(s) in which to perform the calculation",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) for which to perform the calculation",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) over which the calculation should be performed",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entities for which the calculation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) for which the calculation should be peformed",
                           Alias = "Value")]
                string[] values,
                [Parameter("Flag indicating whether to force a calculation when not needed",
                           DefaultValue = false)]
                bool force,
                IOutput output)
        {
            DoCalcOp("Caclulating", metadata, scenarios, years, periods, entities, values, output,
                     (scenario, year, period, entity, value) =>
                {
                    HFM.Try("Calculating",
                            () => HsvCalculate.ChartLogic(scenario.Id, year.Id, period.Id,
                                                          entity.Id, entity.ParentId, value.Id,
                                                          force));
                }
            );
        }


        [Command("Performs a translation")]
        public void Translate(
                Metadata metadata,
                [Parameter("The scenario(s) in which to perform the translation",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) for which to perform the translation",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) over which the translation should be performed",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entities for which the translation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) for which the translation should be peformed",
                           Alias = "Value")]
                string[] values,
                [Parameter("Flag indicating whether to force a translation when not needed",
                           DefaultValue = false)]
                bool force,
                IOutput output)
        {
            DoCalcOp("Translating", metadata, scenarios, years, periods, entities, values, output,
                     (scenario, year, period, entity, value) =>
                {
                    HFM.Try("Translating",
                            () => HsvCalculate.Translate(scenario.Id, year.Id, period.Id,
                                                         entity.Id, entity.ParentId, value.Id,
                                                         force, true));
                }
            );
        }


        [Command("Performs a consolidation")]
        public void Consolidate(
                Metadata metadata,
                [Parameter("The scenario(s) in which to perform the consolidation",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) for which to perform the consolidation",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) over which the consolidation should be performed",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entities for which the consolidation should be performed",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The type of consolidation to perform",
                           DefaultValue = EConsolidationType.Impacted)]
                EConsolidationType consolidationType,
                SystemInfo si,
                IOutput output)
        {
            var scenMembers = metadata["Scenario"].GetMembers(scenarios);
            var yearMembers = metadata["Year"].GetMembers(years);
            var periodMembers = metadata["Period"].GetMembers(periods);
            var entityMembers = metadata["Entity"].GetMembers(entities);

            // Cross-join the selections for scenario, year, and entity
            var loop =
              from s in scenMembers
              from y in yearMembers
              from e in entityMembers
              from p in periodMembers
              select new { Scenario = s, Year = y, Period = p, Entity = e };

            foreach(var i in loop) {
                if(CommandLine.UI.Interrupted) {
                    break;
                }
                _log.InfoFormat("Consolidating Scenario: {0}, Year: {1}, Period: {2}, Entity: {3}",
                        i.Scenario.Name, i.Year.Name, i.Period.Name, i.Entity.Name);
                si.MonitorBlockingTask(output);
                HFM.Try("Consolidating {0}:{1}:{2}:{3}", i.Scenario.Name, i.Year.Name, i.Period.Name,
                        i.Entity.Name,
                        () => HsvCalculate.Consolidate(i.Scenario.Id, i.Year.Id, i.Period.Id,
                                                       i.Entity.Id, i.Entity.ParentId,
                                                       (short)consolidationType));
                si.BlockingTaskComplete();
            }
        }


        [Command("Performs an Equity Pick-up adjustment calculation",
                 Since = "11.1.2.2")]
        public void CalcEPU(
                Metadata metadata,
                [Parameter("The scenario in which to perform the equity pick-up",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year for which to perform the equity pick-up",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) over which the equity pick-up should be performed",
                           Alias = "Period")]
                string[] periods,
                [Parameter("Flag indicating whether to recalculate equity pick-up for all owner-owned pairs, " +
                           "or only those pairs that have been impacted",
                           DefaultValue = false)]
                bool force,
                IOutput output)
        {
            var scenMembers = metadata["Scenario"].GetMembers(scenarios);
            var yearMembers = metadata["Year"].GetMembers(years);
            var periodMembers = metadata["Period"].GetMembers(periods);

            var loop =
              from s in scenMembers
              from y in yearMembers
              from p in periodMembers
              select new { Scenario = s, Year = y, Period = p };

            // Calculate number of iterations, and measure progress
            var members = loop.ToArray();

            output.InitProgress("Equity Pick-Up", members.Length);
            var complete = 0;
            foreach(var m in members) {
                _log.InfoFormat("Equity Pick-Up for Scenario: {0}, Year: {1}, Period: {2}",
                        m.Scenario, m.Year, m.Period);
                HFM.Try("Equity Pick-Up for {0}:{1}:{2}", m.Scenario.Name, m.Year.Name, m.Period.Name,
                        () => HsvCalculate.CalcEPU(m.Scenario.Id, m.Year.Id, m.Period.Id, force));
                if(output.SetProgress(++complete)) {
                    break;
                }
            }
            output.EndProgress();
        }

    }
}
