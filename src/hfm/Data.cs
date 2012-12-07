using System;
using System.Collections.Generic;
using System.Linq;

using log4net;
#if !LATE_BIND
using HSVDATALib;
#endif
using HFMCONSTANTSLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Enumeration of calculation statuses.
    /// </summary>
    public enum ECalcStatus
    {
        AdjIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_ADJ_IS_NODATA,
        AdjNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_ADJ_NEEDS_CALC,
        ConsolidationTransactionsAreInvalid = tagCALCULATIONSTATUS.CALCSTATUS_CONSOLIDATION_TRANSACTIONS_ARE_INVALID,
        ContributionAdjIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_CONTRIBUTIONADJ_IS_NODATA,
        ContributionAdjNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_CONTRIBUTIONADJ_NEEDS_CALC,
        EliminationIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_ELIMINATION_IS_NODATA,
        EliminationNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_ELIMINATION_NEEDS_CALC,
        InputIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_INPUT_IS_NODATA,
        InputNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_INPUT_NEEDS_CALC,
        InUse = tagCALCULATIONSTATUS.CALCSTATUS_INUSE,
        Locked = tagCALCULATIONSTATUS.CALCSTATUS_LOCKED,
        NeedsCalculate = tagCALCULATIONSTATUS.CALCSTATUS_NEEDSCHARTLOGIC,
        NeedsConsolidation = tagCALCULATIONSTATUS.CALCSTATUS_NEEDSCONSOLIDATION,
        NeedsTranslation = tagCALCULATIONSTATUS.CALCSTATUS_NEEDSTRANSLATION,
        NoData = tagCALCULATIONSTATUS.CALCSTATUS_NODATA,
        OK = 0,
        OKButSystemChanged = tagCALCULATIONSTATUS.CALCSTATUS_OK_BUT_SYSTEM_CHANGED,
        ParentAdjIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_PARENTADJ_IS_NODATA,
        ParentAdjNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_PARENTADJ_NEEDS_CALC,
        ProcessFlowBit1 = tagCALCULATIONSTATUS.CALCSTATUS_PROCESS_FLOW_BIT1,
        ProcessFlowBit2 = tagCALCULATIONSTATUS.CALCSTATUS_PROCESS_FLOW_BIT2,
        ProcessFlowBit3 = tagCALCULATIONSTATUS.CALCSTATUS_PROCESS_FLOW_BIT3,
        ProcessFlowBit4 = tagCALCULATIONSTATUS.CALCSTATUS_PROCESS_FLOW_BIT4,
        ProportionIsNoData = tagCALCULATIONSTATUS.CALCSTATUS_PROPORTION_IS_NODATA,
        ProportionNeedsCalc = tagCALCULATIONSTATUS.CALCSTATUS_PROPORTION_NEEDS_CALC
    }


    public static class ECalcStatusExtensions
    {
        public static bool IsSet(this ECalcStatus status, int cellStatus)
        {
            if(status == ECalcStatus.OK) {
                // OK is 0, cell status must be 0 as well to match
                return cellStatus == (int)ECalcStatus.OK;
            }
            else {
                return (cellStatus & (int)status) == (int)status;
            }
        }


        public static IEnumerable<ECalcStatus> GetCellStatuses(int cellStatus)
        {
            var allStatuses = (ECalcStatus[])Enum.GetValues(typeof(ECalcStatus));
            return allStatuses.Where(cs => (cellStatus == 0 && cs == ECalcStatus.OK) ||
                                           (cellStatus != 0 && (cellStatus & (int)cs) == (int)cs));
        }
    }



    /// <summary>
    /// Class for interacting with data / cells in an HFM application.
    /// </summary>
    public class Data
    {

        [Setting("POV", "A Point-of-View expression, such as 'S#Actual.Y#2010.P#May." +
                 "W#YTD.E#E1.V#<Entity Currency>'. Use a POV expression to select members " +
                 "from multiple dimensions in one go. Note that if a dimension member is " +
                 "specified in the POV expression and via a setting for the dimension, the " +
                 "dimension setting takes precedence.",
                 ParameterType = typeof(string), Order = 0),
         Setting("Scenario", "Scenario member(s) to include in the slice definition",
                 Alias = "Scenarios", ParameterType = typeof(string), Order = 2),
         Setting("Year", "Year member(s) to include in the slice definition",
                 Alias = "Years", ParameterType = typeof(string), Order = 3),
         Setting("Period", "Period member(s) to include in the slice definition",
                 Alias = "Periods", ParameterType = typeof(string), Order = 4),
         Setting("View", "View member(s) to include in the slice definition",
                 Alias = "Views", ParameterType = typeof(string), Order = 5,
                 DefaultValue = "<Scenario View>"),
         Setting("Entity", "Entity member(s) to include in the slice definition",
                 Alias = "Entities", ParameterType = typeof(string), Order = 6),
         Setting("Value", "Value member(s) to include in the slice definition",
                 Alias = "Values", ParameterType = typeof(string), Order = 7),
         Setting("Account", "Account member(s) to include in the slice definition",
                 Alias = "Accounts", ParameterType = typeof(string), Order = 8),
         Setting("ICP", "ICP member(s) to include in the slice definition",
                 Alias = "ICPs", ParameterType = typeof(string), Order = 9),
         DynamicSetting("CustomDimName", "<CustomDimName> member(s) to include in the slice definition",
                 ParameterType = typeof(string), Order = 10)]
        public class Cells : Slice, IDynamicSettingsCollection
        {
            [Factory(SingleUse = true)]
            public Cells(Metadata metadata) : base(metadata) {}
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to the Session object
        private readonly Session _session;
        // Reference to the Metadata object
        private readonly Metadata _metadata;
        // Reference to the HsvData module
#if LATE_BIND
        private readonly dynamic _hsvData;
#else
        private readonly HsvData _hsvData;
#endif


        public Data(Session session)
        {
            _log.Trace("Constructing Data object");
            _session = session;
            _metadata = session.Metadata;
#if LATE_BIND
            _hsvData = session.HsvSession.Data;
#else
            _hsvData = (HsvData)session.HsvSession.Data;
#endif
        }


        [Command("Locks the cells in the specified sub-cube(s); once a sub-cube is locked, " +
                 "data cannot be changed again until the sub-cube(s) are unlocked.")]
        public void Lock(
                [Parameter("The scenario(s) to lock",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) to lock",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) to lock",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entity member(s) to lock",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) to lock",
                           Alias = "Value")]
                string[] values,
                IOutput output)
        {
            var ops = _metadata.DoSubcubeOp("Locking", scenarios, years, periods, entities, values, output,
                                            (pov) => _hsvData.SetCalcStatusLocked(pov.Scenario.Id, pov.Year.Id,
                                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId,
                                                        pov.Value.Id));
            _log.InfoFormat("Lock complete: {0} performed", ops);
        }


        [Command("Unlocks the cells in the specified sub-cube(s)")]
        public void Unlock(
                [Parameter("The scenario(s) to unlock",
                           Alias = "Scenario")]
                string[] scenarios,
                [Parameter("The year(s) to unlock",
                           Alias = "Year")]
                string[] years,
                [Parameter("The period(s) to unlock",
                           Alias = "Period")]
                string[] periods,
                [Parameter("The entity member(s) to unlock",
                           Alias = "Entity")]
                string[] entities,
                [Parameter("The value member(s) to unlock",
                           Alias = "Value")]
                string[] values,
                IOutput output)
        {
            var ops = _metadata.DoSubcubeOp("Unlocking", scenarios, years, periods, entities, values, output,
                                            (pov) => _hsvData.SetCalcStatusUnlocked(pov.Scenario.Id, pov.Year.Id,
                                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId,
                                                        pov.Value.Id));
            _log.InfoFormat("Unlock complete: {0} performed", ops);
        }


        [Command("Sets a cell or slice to the specified value (note: use ClearCell to remove data). " +
                 "The cell intersection(s) to be set can be specified using either a POV string, " +
                 "a member list specification for each dimension, or a combination of both - provided " +
                 "all dimensions are ultimately specified. If a dimension is specified both in the POV " +
                 "and in a dimension setting, the dimension setting takes precedence.")]
        public void SetCell(
                Cells slice,
                [Parameter("The amount to set the cell(s) to")]
                double amount,
                IOutput output)
        {
            SetCellInternal(slice, amount, false, output);
        }


        [Command("Clears data fron a cell or slice. " +
                 "The cell intersection(s) to be cleared can be specified using either a POV string, " +
                 "a member list specification for each dimension, or a combination of both - provided " +
                 "all dimensions are ultimately specified. If a dimension is specified both in the POV " +
                 "and in a dimension setting, the dimension setting takes precedence.")]
        public void ClearCell(
                Cells slice,
                IOutput output)
        {
            SetCellInternal(slice, 0, true, output);
        }


        [Command("Retrieves the value(s) from a cell or slice. " +
                 "The cell intersection(s) to be set can be specified using either a POV string, " +
                 "a member list specification for each dimension, or a combination of both - provided " +
                 "all dimensions are ultimately specified. If a dimension is specified both in the POV " +
                 "and in a dimension setting, the dimension setting takes precedence.")]
        public void GetCell(
                Cells slice,
                IOutput output)
        {
            var POVs = slice.POVs;
            output.InitProgress("Retrieving cells", POVs.Length);
            output.SetHeader("POV", 50, "Value", 15);
            foreach(var pov in POVs) {
                var val = GetCellValue(pov);
                output.WriteRecord(pov, string.Format("{0,-15}", val == null ? "-" : val.ToString()));
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
        }


        protected void SetCellInternal(Cells slice, double amount, bool clear, IOutput output)
        {
            var POVs = slice.POVs;
            output.InitProgress(string.Format("{0} cells", clear ? "Clearing" : "Setting"), POVs.Length);
            foreach(var pov in POVs) {
                if(HFM.HasVariableCustoms) {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCellExtDim(pov.HfmPovCOM, amount, clear));
                }
                else {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCell(pov.Scenario.Id, pov.Year.Id, pov.Period.Id, pov.View.Id,
                                                   pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                                   pov.Account.Id, pov.ICP.Id, pov.Custom1.Id,
                                                   pov.Custom2.Id, pov.Custom3.Id, pov.Custom4.Id,
                                                   amount, clear));
                }
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
        }


        /// Returns a bit-field representing the calculation status for a subcube
        internal int GetCalcStatus(POV pov)
        {
            int status = -1;
            int valueId = pov.IsSpecified(EDimension.Value) ? pov.Value.Id :
                                                              pov.Entity.DefaultCurrencyId;
            HFM.Try("Retrieving calc status for {0}", pov,
                    () => _hsvData.GetCalcStatus(pov.Scenario.Id, pov.Year.Id, pov.Period.Id,
                                                 pov.Entity.Id, pov.Entity.ParentId, valueId,
                                                 out status));
            return status;
        }


        /// Returns the data value in the specified cell, or null if the cell
        /// contains no data
        internal double? GetCellValue(POV pov)
        {
            double amount = 0;
            int status = 0;
            if(HFM.HasVariableCustoms) {
                HFM.Try("Getting cell data value for {0}", pov,
                        () => _hsvData.GetCellExtDim(pov.HfmPovCOM, out amount, out status));
            }
            else {
                HFM.Try("Getting cell data value for {0}", pov,
                        () => _hsvData.GetCell(pov.Scenario.Id, pov.Year.Id, pov.Period.Id, pov.View.Id,
                                               pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                               pov.Account.Id, pov.ICP.Id, pov.Custom1.Id,
                                               pov.Custom2.Id, pov.Custom3.Id, pov.Custom4.Id,
                                               out amount, out status));
            }
            if(ECalcStatus.NoData.IsSet(status)) {
                return null;
            }
            else {
                return amount;
            }
        }

    }

}
