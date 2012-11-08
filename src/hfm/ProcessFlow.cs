using System;
using System.Linq;

using log4net;
using HSVSESSIONLib;
using HSVPROCESSFLOWLib;
using HFMCONSTANTSLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Enumeration of process management states
    /// </summary>
    public enum EProcessState : short
    {
        NotSupported = CEnumProcessFlowStates.PROCESS_FLOW_STATE_NOT_SUPPORTED,
        NotStarted = CEnumProcessFlowStates.PROCESS_FLOW_STATE_NOT_STARTED,
        FirstPass = CEnumProcessFlowStates.PROCESS_FLOW_STATE_FIRST_PASS,
        ReviewLevel1 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW1,
        ReviewLevel2 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW2,
        ReviewLevel3 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW3,
        ReviewLevel4 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW4,
        ReviewLevel5 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW5,
        ReviewLevel6 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW6,
        ReviewLevel7 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW7,
        ReviewLevel8 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW8,
        ReviewLevel9 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW9,
        ReviewLevel10 = CEnumProcessFlowStates.PROCESS_FLOW_STATE_REVIEW10,
        Submitted = CEnumProcessFlowStates.PROCESS_FLOW_STATE_SUBMITTED,
        Approved = CEnumProcessFlowStates.PROCESS_FLOW_STATE_APPROVED,
        Published = CEnumProcessFlowStates.PROCESS_FLOW_STATE_PUBLISHED
    }


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


        [Command("Returns the current process state")]
        public void EnumProcessState(Slice slice, IOutput output)
        {
            if(_usesPhasedSubmissions) {
                GetPhasedSubmissionProcessState(slice, output);
            }
            else {
                GetProcessState(slice, output);
            }
        }


        private void GetProcessState(Slice slice, IOutput output)
        {
            short state = 0;

            output.SetHeader("Process Unit", "Process State");
            foreach(var pu in slice.ProcessUnits) {
                HFM.Try("Retrieving process state",
                        () => _hsvProcessFlow.GetState(pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                       pu.Entity.Id, pu.Entity.ParentId, pu.Value.Id,
                                       out state));
                output.WriteRecord(pu.ProcessUnitLabel, (EProcessState)state);
            }
            output.End();
        }


        private void GetPhasedSubmissionProcessState(Slice slice, IOutput output)
        {
            short state = 0;

            // Default each dimension for which phased submission is not enabled
            foreach(var id in _metadata.CustomDimIds) {
                if(!_metadata.IsPhasedSubmissionEnabledForDimension(id)) {
                    slice[id] = new MemberList(_metadata[id], "[None]");
                }
            }

            output.SetHeader("POV", "Process State");
            foreach(var pov in slice.POVs) {
                if(_metadata.HasVariableCustoms) {
                    HFM.Try("Retrieving phased submission process state",
                            () => _hsvProcessFlow.GetPhasedSubmissionStateExtDim(pov.HfmPovCOM, out state));
                }
                else {
                    HFM.Try("Retrieving phased submission process state",
                            () => _hsvProcessFlow.GetPhasedSubmissionState(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId,
                                        pov.Value.Id, pov.Account.Id, pov.ICP.Id, pov.Custom1.Id,
                                        pov.Custom2.Id, pov.Custom3.Id, pov.Custom4.Id, out state));
                }
                output.WriteRecord(pov, (EProcessState)state);
            }
            output.End();
        }


        [Command("Returns the submission group and submission phase for each cell in the slice")]
        public void EnumGroupPhases(Slice slice, IOutput output)
        {
            string group = null, phase = null;
            // TODO: Should we check if phased submissions are enabled?
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
