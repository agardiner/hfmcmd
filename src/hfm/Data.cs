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


        protected void SetCellInternal(Metadata metadata, Slice slice, double value, bool clear, IOutput output)
        {
            if(metadata.HasVariableCustoms) {
                var povs = slice.POVs().ToArray();
                output.InitProgress(string.Format("{0} cells", clear ? "Clearing" : "Setting"), povs.Length);
                foreach(var cellPOV in slice.POVs()) {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCellExtDim(cellPOV, value, clear));
                    if(output.IterationComplete()) {
                        break;
                    }
                }
                output.EndProgress();
            }
            else {
                var cells = slice.Cells().ToArray();
                output.InitProgress(string.Format("{0} cells", clear ? "Clearing" : "Setting"), cells.Length);
                foreach(var cell in slice.Cells()) {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCell(cell.Scenario.Id, cell.Year.Id, cell.Period.Id, cell.View.Id,
                                                   cell.Entity.Id, cell.Entity.ParentId, cell.Value.Id,
                                                   cell.Account.Id, cell.ICP.Id, cell.Custom1.Id,
                                                   cell.Custom2.Id, cell.Custom3.Id, cell.Custom4.Id,
                                                   value, clear));
                    if(output.IterationComplete()) {
                        break;
                    }
                }
                output.EndProgress();
            }
        }

    }

}
