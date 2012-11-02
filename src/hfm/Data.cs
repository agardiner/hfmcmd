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


        [Command("Sets a cell or slice to the specified value")]
        public void SetCell(
                Metadata metadata,
                Slice slice,
                [Parameter("The value to set the cell to")]
                double amount,
                IOutput output)
        {
            SetCellInternal(metadata, slice, amount, false, output);
        }


        [Command("Clears data fron a cell or slice")]
        public void ClearCell(
                Metadata metadata,
                Slice slice,
                IOutput output)
        {
            SetCellInternal(metadata, slice, 0, true, output);
        }


        protected void SetCellInternal(Metadata metadata, Slice slice, double amount, bool clear, IOutput output)
        {
            var povs = slice.POVs;
            output.InitProgress(string.Format("{0} cells", clear ? "Clearing" : "Setting"), povs.Length);
            foreach(var pov in povs) {
                if(metadata.HasVariableCustoms) {
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
