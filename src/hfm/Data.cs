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
            if(Command.VersionedAttribute.ConvertVersionStringToNumber(HFM.Version) >=
               Command.VersionedAttribute.ConvertVersionStringToNumber("11.1.2.2")) {
                HFM.Try("Setting cell",
                        () => _hsvData.SetCellExtDim(metadata.POV.FromPOVString(pov), value, clear));
            }
            else {
                var cell = metadata.POV;
                HFM.Try("Setting cell",
                        () => _hsvData.SetCell(cell.ScenarioID, cell.YearID, cell.PeriodID, cell.ViewID,
                                               cell.EntityID, cell.ParentID, cell.ValueID,
                                               cell.AccountID, cell.ICPID, cell.Custom1ID,
                                               cell.Custom2ID, cell.Custom3ID, cell.Custom4ID,
                                               value, clear));
            }
        }

    }

}
