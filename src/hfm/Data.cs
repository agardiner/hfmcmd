using System;
using System.Linq;

using log4net;
using HSVDATALib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// </summary>
    public class Data
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to the HsvData module
        private HsvData _hsvData;


        [Factory]
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

    }

}
