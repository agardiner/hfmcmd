using System;
using System.Linq;
using System.Text.RegularExpressions;

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
    /// Enumeration of process management actions
    /// </summary>
    public enum EProcessAction
    {
        Approve = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_APPROVE,
        Promote = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_PROMOTE,
        Publish = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_PUBLISH,
        Reject  = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_REJECT,
        SignOff = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_SIGN_OFF,
        Start   = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_START,
        Submit  = CEnumProcessFlowActions.PROCESS_FLOW_ACTION_SUBMIT
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
                GetProcessUnitState(slice, output);
            }
        }


        [Command("Returns the submission group and submission phase for each cell in the slice")]
        public void EnumGroupPhases(Slice slice, IOutput output)
        {
            string group = null, phase = null;

            output.SetHeader("Cell", 50, "Submission Group", "Submission Phase", 20);
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


        [Command("Starts a process unit or phased submission, moving it from Not Started to First Pass")]
        public void Start(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Start, EProcessState.FirstPass,
                        annotation, attachments, output);
            }
        }


        [Command("Promotes a process unit or phased submission to the specified Review level")]
        public void Promote(
                Slice slice,
                [Parameter("Review level to promote to (a value between 1 and 10)")]
                string reviewLevel,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            var re = new Regex("^(?:ReviewLevel)?([0-9]|10)$", RegexOptions.IgnoreCase);
            var match = re.Match(reviewLevel);
            EProcessState targetState;
            if(match.Success) {
                targetState = (EProcessState)Enum.Parse(typeof(EProcessState), "ReviewLevel" + match.Groups[1].Value);
            }
            else {
                throw new ArgumentException("Review level must be a value between 1 and 10");
            }

            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Promote, targetState,
                        annotation, attachments, output);
            }
        }


        [Command("Returns a process unit or phased submission to its prior state")]
        public void Reject(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Reject, EProcessState.NotSupported,
                        annotation, attachments, output);
            }
        }


        [Command("Sign-off a process unit or phased submission. This records sign-off of the data " +
                 "at the current point in the process (which must be a review level), but does not " +
                 "otherwise change the process level of the process unit or phased submission.")]
        public void SignOff(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.SignOff, EProcessState.NotSupported,
                        annotation, attachments, output);
            }
        }


        [Command("Submit a process unit or phased submission for approval.")]
        public void Submit(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Submit, EProcessState.Submitted,
                        annotation, attachments, output);
            }
        }


        [Command("Approve a process unit or phased submission.")]
        public void Approve(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Approve, EProcessState.Approved,
                        annotation, attachments, output);
            }
        }


        [Command("Publish a process unit or phased submission group.")]
        public void Publish(
                Slice slice,
                [Parameter("Annotations to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string annotation,
                [Parameter("File attachment(s) to be applied to each process unit or phased submission",
                           DefaultValue = null)]
                string[] attachments,
                IOutput output)
        {
            if(_usesPhasedSubmissions) {
                //SetPhasedSubmissionProcessState(slice, output);
            }
            else {
                SetProcessUnitState(slice, EProcessAction.Publish, EProcessState.Published,
                        annotation, attachments, output);
            }
        }


        private void GetProcessUnitState(Slice slice, IOutput output)
        {
            short state = 0;

            output.SetHeader("Process Unit", 58, "Process State", 15);
            foreach(var pu in slice.ProcessUnits) {
                HFM.Try("Retrieving process state",
                        () => _hsvProcessFlow.GetState(pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                       pu.Entity.Id, pu.Entity.ParentId, pu.Value.Id,
                                       out state));
                output.WriteRecord(pu.ProcessUnitLabel, (EProcessState)state);
            }
            output.End();
        }


        private void SetProcessUnitState(Slice slice, EProcessAction action, EProcessState targetState,
                string annotation, string[] documents, IOutput output)
        {
            bool allValues = true, allPeriods = true;
            string[] paths = null, files = null;
            short newState = 0;

            var PUs = slice.ProcessUnits;
            output.InitProgress("Processing " + action.ToString(), PUs.Length);
            foreach(var pu in PUs) {
                HFM.Try("Setting process unit state",
                        () => _hsvProcessFlow.ProcessManagementChangeStateForMultipleEntities2(
                                    pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                    new int[] { pu.Entity.Id }, new int[] { pu.Entity.ParentId },
                                    pu.Value.Id, annotation, (int)action, allValues, allPeriods,
                                    (short)targetState, paths, files, out newState));
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
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

            output.SetHeader("POV", 58, "Process State", 15);
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


        private void SetPhasedSubmissionProcessState()
        {
            //PhasedSubmissionProcessManagementChangeStateForMultipleEntities2ExtDim
            //PhasedSubmissionProcessManagementChangeStateForMultipleEntities2
        }

    }

}
