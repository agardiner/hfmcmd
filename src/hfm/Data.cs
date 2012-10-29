using System;

using log4net;
using HSVDATALib;

using Command;


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


        [Command("Sets a cell to the specified value")]
        public void SetCell(
                Metadata metadata,
                [Parameter("The POV string, e.g. 'S#Actual.Y#2012.P#Jan." +
                           "W#YTD.E#E100.V#<Entity Currency>.A#A1234.C1#[None].C2#[None]'")]
                string pov,
                [Parameter("The value to set the cell to")]
                double value)
        {
            SetCellInternal(metadata, pov, value, false);
        }


        [Command("Clears data fron a cell")]
        public void ClearCell(
                Metadata metadata,
                [Parameter("The POV string, e.g. 'S#Actual.Y#2012.P#Jan." +
                           "W#YTD.E#E100.V#<Entity Currency>.A#A1234.C1#[None].C2#[None]'")]
                string pov)
        {
            SetCellInternal(metadata, pov, 0, true);
        }


        protected void SetCellInternal(Metadata metadata, string pov, double value, bool clear)
        {
            Slice slice = metadata.Slice(pov);
            if(Command.VersionedAttribute.ConvertVersionStringToNumber(HFM.Version) >=
               Command.VersionedAttribute.ConvertVersionStringToNumber("11.1.2.2")) {
                foreach(var cellPOV in slice.POVs()) {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCellExtDim(cellPOV, value, clear));
                }
            }
            else {
                foreach(var cell in slice.Cells()) {
                    HFM.Try("Setting cell",
                            () => _hsvData.SetCell(cell.Scenario.Id, cell.Year.Id, cell.Period.Id, cell.View.Id,
                                                   cell.Entity.Id, cell.Entity.ParentId, cell.Value.Id,
                                                   cell.Account.Id, cell.ICP.Id, cell.Custom1.Id,
                                                   cell.Custom2.Id, cell.Custom3.Id, cell.Custom4.Id,
                                                   value, clear));
                }
            }
        }

    }

}
