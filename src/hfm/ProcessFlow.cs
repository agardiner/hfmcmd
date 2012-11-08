using System;

using log4net;
using HSVSESSIONLib;
using HSVPROCESSFLOWLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Performs process management on an HFM application.
    /// </summary>
    public class ProcessFlow
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvProcessFlow object
        private HsvProcessFlow _hsvProcessFlow;
        // Reference to Metadata object
        private Metadata _metadata;
        // Flag indicating whether application uses phased submission groups
        private bool _usesPhasedSubmissions;


        [Factory]
        public ProcessFlow(Session session, Metadata metadata)
        {
            _hsvProcessFlow = (HsvProcessFlow)session.HsvSession.ProcessFlow;
            _metadata = metadata;
            _usesPhasedSubmissions = metadata.UsesPhasedSubmissions;
        }


        [Command("Returns the submission group and submission phase for each cell in the slice")]
        public void EnumGroupPhases(Slice slice, IOutput output)
        {
            string group = null, phase = null;
            output.SetHeader("Cell", "Submission Group", "Submission Phase");
            foreach(var pov in slice.POVs) {
                if(_metadata.HasVariableCustoms) {
                    HFM.Try("Retrieving submission group and phase",
                            () => _hsvProcessFlow.GetGroupPhaseFromCellExtDim(pov.HfmPovCOM,
                                         out group, out phase));
                }
                else {
                    HFM.Try("Retrieving submission group and phase",
                            () => _hsvProcessFlow.GetGroupPhaseFromCell(pov.Scenario.Id, pov.Year.Id,
                                         pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                         pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                         pov.Custom3.Id, pov.Custom4.Id, out group, out phase));
                }
                output.WriteRecord(pov, group, phase);
            }
            output.End();
        }

    }

}
