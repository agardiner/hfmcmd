using System;
using System.Collections.Generic;
using System.Linq;

using log4net;
using HSVDATALib;
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
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to the HsvData module
        private HsvData _hsvData;


        public Data(Session session)
        {
            _hsvData = (HsvData)session.HsvSession.Data;
        }


        [Command("Sets a cell or slice to the specified value (note: use ClearCell to remove data). " +
                 "The cell intersection(s) to be set can be specified using either a POV string, " +
                 "a member list specification for each dimension, or a combination of both - provided " +
                 "all dimensions are ultimately specified. If a dimension is specified both in the POV " +
                 "and in a dimension setting, the dimension setting takes precedence.")]
        public void SetCell(
                Slice slice,
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
                Slice slice,
                IOutput output)
        {
            SetCellInternal(slice, 0, true, output);
        }


        protected void SetCellInternal(Slice slice, double amount, bool clear, IOutput output)
        {
            var povs = slice.POVs;
            output.InitProgress(string.Format("{0} cells", clear ? "Clearing" : "Setting"), povs.Length);
            foreach(var pov in povs) {
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
            int status = 0;
            HFM.Try("Retrieving calc status",
                    () => _hsvData.GetCalcStatus(pov.Scenario.Id, pov.Year.Id, pov.Period.Id,
                                                 pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                                 out status));
            return status;
        }


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
