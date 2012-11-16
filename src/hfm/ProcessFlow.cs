using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

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
    public enum EProcessAction : short
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
    public abstract class ProcessFlow
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsvProcessFlow object
        protected HsvProcessFlow _hsvProcessFlow;
        // Refernece to the Session object
        protected Session _session;
        // Reference to Metadata object
        protected Metadata _metadata;
        // Reference to Security object
        protected Security _security;


        /// Constructor
        internal ProcessFlow(Session session)
        {
            _session = session;
            _hsvProcessFlow = (HsvProcessFlow)session.HsvSession.ProcessFlow;
            _metadata = session.Metadata;
            _security = session.Security;
        }


        [Command("Returns the current process state")]
        public void EnumProcessState(Slice slice, IOutput output)
        {
            GetProcessState(slice, output);
        }


        [Command("Returns the submission group and submission phase for each cell in the slice")]
        public void EnumGroupPhases(Slice slice, IOutput output)
        {
            int group, phase;
            DefaultMembers(slice, false);
            output.SetHeader("Cell", 50, "Submission Group", "Submission Phase", 20);
            foreach(var pov in slice.POVs) {
                GetGroupPhase(pov, out group, out phase);
                output.WriteRecord(pov, group, phase);
            }
            output.End();
        }


        protected void GetGroupPhase(POV pov, out int group, out int phase)
        {
            string sGroup = null, sPhase = null;
            if(HFM.HasVariableCustoms) {
                HFM.Try("Retrieving submission group and phase",
                        () => _hsvProcessFlow.GetGroupPhaseFromCellExtDim(pov.HfmPovCOM,
                                     out sGroup, out sPhase));
            }
            else {
                HFM.Try("Retrieving submission group and phase",
                        () => _hsvProcessFlow.GetGroupPhaseFromCell(pov.Scenario.Id, pov.Year.Id,
                                     pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                     pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                     pov.Custom3.Id, pov.Custom4.Id, out sGroup, out sPhase));
            }
            group = int.Parse(sGroup);
            phase = int.Parse(sPhase);
        }


        [Command("Returns the history of process management actions performed on process units")]
        public void GetProcessHistory(Slice slice, IOutput output)
        {
            POV[] PUs = GetProcessUnits(slice);
            foreach(var pu in PUs) {
                GetHistory(pu, output);
            }
        }


        /// Method to be implemented in sub-classes for retrieving the state of
        /// process unit(s) represented by the Slice.
        protected abstract void GetHistory(POV processUnit, IOutput output);


        protected void OutputHistory(IOutput output, POV pu, object oDates, object oUsers,
                object oActions, object oStates, object oAnnotations, object oPaths, object oFiles)
        {
            var dates = (double[])oDates;
            var users = HFM.Object2Array<string>(oUsers);
            var actions = (EProcessAction[])oActions;
            var states = (EProcessState[])oStates;
            var annotations = HFM.Object2Array<string>(oAnnotations);
            var paths = HFM.Object2Array<string>(oPaths);
            var files = HFM.Object2Array<string>(oFiles);

            output.WriteLine("Process history for {0} {1}:", ProcessUnitType, pu);
            output.SetHeader("Date", "User", 30, "Action", 10, "Process State", 14, "Annotation");
            for(int i = 0; i < dates.Length; ++i) {
                output.WriteRecord(DateTime.FromOADate(dates[i]), users[i], actions[i], states[i], annotations[i]);
            }
            output.End(true);
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
            SetProcessState(slice, EProcessAction.Start, ERole.ProcessFlowSupervisor,
                    EProcessState.FirstPass, annotation, attachments, false, output);
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
            ERole roleNeeded;
            if(match.Success) {
                targetState = (EProcessState)Enum.Parse(typeof(EProcessState), "ReviewLevel" + match.Groups[1].Value);
                roleNeeded = (ERole)Enum.Parse(typeof(ERole), "ProcessFlowRole" + (int.Parse(match.Groups[1].Value) - 1));
            }
            else {
                throw new ArgumentException("Review level must be a value between 1 and 10");
            }

            SetProcessState(slice, EProcessAction.Promote, roleNeeded, targetState,
                    annotation, attachments, false, output);
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
            SetProcessState(slice, EProcessAction.Reject, ERole.ProcessFlowReviewer1, EProcessState.NotSupported,
                    annotation, attachments, false, output);
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
            SetProcessState(slice, EProcessAction.SignOff, ERole.ProcessFlowReviewer1, EProcessState.NotSupported,
                    annotation, attachments, false, output);
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
            SetProcessState(slice, EProcessAction.Submit, ERole.ProcessFlowSubmitter, EProcessState.Submitted,
                    annotation, attachments, false, output);
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
            SetProcessState(slice, EProcessAction.Approve, ERole.ProcessFlowSupervisor, EProcessState.Approved,
                    annotation, attachments, false, output);
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
                [Parameter("Consolidate the process unit if consolidation is necessary to publish.",
                           DefaultValue = true)]
                bool consolidateIfNeeded,
                IOutput output)
        {
            SetProcessState(slice, EProcessAction.Publish, ERole.ProcessFlowSupervisor, EProcessState.Published,
                    annotation, attachments, consolidateIfNeeded, output);
        }


        /// Property to return a label for a unit of process management
        protected abstract string ProcessUnitType { get; }


        /// Method to be implemented in sub-classes to return an array of POV
        /// instances for each process unit represented by the slice.
        protected abstract POV[] GetProcessUnits(Slice slice);


        /// Method to be implemented in sub-classes for retrieving the state of
        /// process unit(s) represented by the Slice.
        protected abstract void GetProcessState(Slice slice, IOutput output);



        /// Method to be implemented in sub-classes for setting the state of
        /// process unit(s) represented by the Slice.
        protected void SetProcessState(Slice slice, EProcessAction action, ERole role,
                EProcessState targetState, string annotation, string[] documents,
                bool consolidateIfNeeded, IOutput output)
        {
            string[] paths = null, files = null;
            int processed = 0, skipped = 0;
            EProcessState state;

            _security.CheckPermissionFor(ETask.ProcessManagement);
            if(role != ERole.Default) {
                _security.CheckRole(role);
            }

            // Convert document references to path(s) to files
            if(documents != null) {
                paths = new string[documents.Length];
                files = new string[documents.Length];
                for(int i = 0; i < documents.Length; ++i) {
                    Utilities.EnsureFileExists(documents[i]);
                    paths[i] = Path.GetDirectoryName(documents[i]);
                    files[i] = Path.GetFileName(documents[i]);
                }
            }

            // Iterate over process units, performing action
            var PUs = GetProcessUnits(slice);
            output.InitProgress("Processing " + action.ToString(), PUs.Length);
            foreach(var pu in PUs) {
                var access = _security.GetProcessUnitAccessRights(pu, out state);
                if(state == targetState) {
                    skipped++;
                }
                else if(IsValidStateTransition(action, pu, state, targetState) &&
                        HasSufficientAccess(action, pu, access, state) &&
                        CanAction(action, pu, consolidateIfNeeded)) {
                    try {
                        SetProcessState(pu, action, targetState, annotation, paths, files);
                        processed++;
                    }
                    catch(HFMException ex) {
                        _log.Error(ex);
                    }
                }
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
            if(processed > 0) {
                _log.InfoFormat("{0} action successful for {1} {2}s", action, processed, ProcessUnitType);
            }
            else if(skipped == 0) {
                throw new Exception(string.Format("Failed to {0} any {1}s", action, ProcessUnitType));
            }
        }


        protected void DefaultMembers(Slice slice, bool overrideExisting)
        {
            // Default each dimension for which phased submission is not enabled
            if(!_metadata.IsPhasedSubmissionEnabledForDimension((int)EDimension.Account) &&
                (overrideExisting || !slice.IsSpecified(EDimension.Account))) {
                slice[EDimension.Account] = "[None]";
            }
            if(!_metadata.IsPhasedSubmissionEnabledForDimension((int)EDimension.ICP) &&
                (overrideExisting || !slice.IsSpecified(EDimension.ICP))) {
                slice[EDimension.ICP] = "[ICP None]";
            }
            foreach(var id in _metadata.CustomDimIds) {
                if(!_metadata.IsPhasedSubmissionEnabledForDimension(id) &&
                    (overrideExisting || !slice.IsSpecified(id))) {
                   slice[id] = "[None]";
                }
            }
        }


        /// Returns true if it is possible to go from start state to end state
        protected bool IsValidStateTransition(EProcessAction action, POV pu,
                EProcessState start, EProcessState end)
        {
            bool ok = false;
            switch(action) {
                case EProcessAction.Start:
                    ok = start == EProcessState.NotStarted;
                    break;
                case EProcessAction.Promote:
                    ok = start >= EProcessState.FirstPass && end < EProcessState.ReviewLevel10 &&
                         end >= EProcessState.ReviewLevel1 && end <= EProcessState.ReviewLevel10;
                    break;
                case EProcessAction.Reject:
                    ok = start != EProcessState.NotStarted;
                    break;
                case EProcessAction.SignOff:
                    ok = start >= EProcessState.ReviewLevel1;
                    break;
                case EProcessAction.Submit:
                    ok = start >= EProcessState.FirstPass && start < EProcessState.Submitted;
                    break;
                case EProcessAction.Approve:
                    ok = start == EProcessState.Submitted;
                    break;
                case EProcessAction.Publish:
                    ok = start >= EProcessState.Submitted && start < EProcessState.Published;
                    break;
            }
            if(!ok) {
                _log.WarnFormat("{0} {1} is in the wrong state ({2}) to {3}",
                        ProcessUnitType.Capitalize(), pu, start, action);
            }
            return ok;
        }


        // Check user has sufficient access rights for action
        protected bool HasSufficientAccess(EProcessAction action, POV pu, EAccessRights access, EProcessState state)
        {
            bool ok = false;
            switch(action) {
                case EProcessAction.Start:
                    ok = access == EAccessRights.All;
                    break;
                case EProcessAction.Promote:
                    ok = access == EAccessRights.Promote || access == EAccessRights.All;
                    break;
                case EProcessAction.Reject:
                    ok = access == EAccessRights.All ||
                         ((access == EAccessRights.Read || access == EAccessRights.Promote) &&
                          state != EProcessState.Published);
                    break;
                case EProcessAction.SignOff:
                    ok = access == EAccessRights.Read || access == EAccessRights.Promote ||
                         access == EAccessRights.All;
                    break;
                case EProcessAction.Submit:
                    ok = access == EAccessRights.Promote || access == EAccessRights.All;
                    break;
                case EProcessAction.Approve:
                    ok = access == EAccessRights.Promote || access == EAccessRights.All;
                    break;
                case EProcessAction.Publish:
                    ok = access == EAccessRights.All;
                    break;
            }
            if(!ok) {
                _log.WarnFormat("Insufficient privileges to change process state for {0} {1}",
                        ProcessUnitType, pu);
            }
            return ok;
        }



        // To be able to change the status of a process unit, it needs to be:
        // unlocked, calculated, and valid
        protected bool CanAction(EProcessAction action, POV pu, bool consolidateIfNeeded)
        {
            bool ok = true;
            if(action == EProcessAction.Promote || action == EProcessAction.SignOff ||
               action == EProcessAction.Submit || action == EProcessAction.Approve ||
               action == EProcessAction.Publish) {
                ok = CheckCalcStatus(action, pu, consolidateIfNeeded);
                ok = ok && CheckValidationStatus(action, pu);
            }
            return ok;
        }


        protected bool CheckCalcStatus(EProcessAction action, POV pu, bool consolidateIfNeeded)
        {
            bool ok = false;
            var calcStatus = _session.Data.GetCalcStatus(pu);
            if(ECalcStatus.OK.IsSet(calcStatus) ||
               ECalcStatus.OKButSystemChanged.IsSet(calcStatus) ||
               ECalcStatus.NoData.IsSet(calcStatus)) {
                if(ECalcStatus.Locked.IsSet(calcStatus)) {
                    _log.ErrorFormat("Cannot {0} {1} {2} as it has been locked",
                            action, ProcessUnitType, pu);
                }
                else {
                    ok = true;
                }
            }
            else if(consolidateIfNeeded) {
                HFM.Try("Consolidating {0}", pu,
                        () => _session.Calculate.HsvCalculate.Consolidate(pu.Scenario.Id, pu.Year.Id,
                                       pu.Period.Id, pu.Entity.Id, pu.Entity.ParentId,
                                       (short)EConsolidationType.Impacted));
            }
            else {
                _log.ErrorFormat("Cannot {0} {1} {2} until it has been consolidated",
                        action, ProcessUnitType, pu);
            }
            return ok;
        }


        protected bool CheckValidationStatus(EProcessAction action, POV pu)
        {
            bool ok = true;
            int group, phase;
            GetGroupPhase(pu, out group, out phase);
            if(phase == 0) {
                // Cell does not participate in process management
                throw new ArgumentException(string.Format("POV {0} is not valid for process management", pu));
            }
            var account = _metadata.GetPhaseValidationAccount(phase);
            if(account.Id != Member.NOT_USED) {
                var pov = pu.Copy();
                // Set validation account POV
                pov.View = _metadata.View.GetMember("<Scenario View>");
                pov.ICP = _metadata.ICP.GetMember("[ICP Top]");
                foreach(var id in _metadata.CustomDimIds) {
                    pov[id] = account.GetTopCustomMember(id);
                }
                var valAmt = _session.Data.GetCellValue(pu);
                ok = valAmt == null || valAmt == 0;
                if(!ok) {
                    _log.ErrorFormat("Cannot {0} {1} {2} until it passes all validations",
                            action, ProcessUnitType, pu);
                }
            }
            return ok;
        }


        /// Method to be implemented in sub-classes for setting the state of a
        /// single process unit represented by the POV.
        protected abstract EProcessState SetProcessState(POV processUnit, EProcessAction action,
                EProcessState targetState, string annotation, string[] paths, string[] files);

    }



    /// <summary>
    /// ProcessFlow subclass for working with applications that use Process Unit
    /// based process management. Process management is applied to process
    /// units, which are combinations of Scenario, Year, Period, Entity and
    /// Value.
    /// </summary>
    public class ProcessUnitProcessFlow : ProcessFlow
    {

        protected override string ProcessUnitType { get { return "process unit"; } }


        internal ProcessUnitProcessFlow(Session session)
            : base(session)
        { }


        protected override POV[] GetProcessUnits(Slice slice)
        {
            DefaultMembers(slice, true);
            return slice.ProcessUnits;
        }


        protected override void GetProcessState(Slice slice, IOutput output)
        {
            short state = 0;

            output.SetHeader("Process Unit", 58, "Process State", 15);
            foreach(var pu in GetProcessUnits(slice)) {
                HFM.Try("Retrieving process state",
                        () => _hsvProcessFlow.GetState(pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                       pu.Entity.Id, pu.Entity.ParentId, pu.Value.Id,
                                       out state));
                output.WriteRecord(pu, (EProcessState)state);
            }
            output.End();
        }


        protected override void GetHistory(POV pu, IOutput output)
        {
            object oDates = null, oUsers = null, oActions = null, oStates = null,
                   oAnnotations = null, oPaths = null, oFiles = null;

            HFM.Try("Retrieving process history",
                    () => _hsvProcessFlow.GetHistory2(pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                    pu.Entity.Id, pu.Entity.ParentId, pu.Value.Id,
                                    out oDates, out oUsers, out oActions, out oStates,
                                    out oAnnotations, out oPaths, out oFiles));
            OutputHistory(output, pu, oDates, oUsers, oActions, oStates,
                    oAnnotations, oPaths, oFiles);
        }


        protected override EProcessState SetProcessState(POV pu, EProcessAction action, EProcessState targetState,
                string annotation, string[] paths, string[] files)
        {
            short newState = 0;

            HFM.Try("Setting process unit state",
                    () => _hsvProcessFlow.ProcessManagementChangeStateForMultipleEntities2(
                                pu.Scenario.Id, pu.Year.Id, pu.Period.Id,
                                new int[] { pu.Entity.Id }, new int[] { pu.Entity.ParentId },
                                pu.Value.Id, annotation, (int)action, false, false,
                                (short)targetState, paths, files, out newState));
            return (EProcessState)newState;
        }

    }



    /// <summary>
    /// ProcessFlow subclass for working with applications that use Phased
    /// Submissions. This allows process management to be applied to sets of
    /// Accounts, ICP members, and Customs, in addition to the standard Scenario
    /// Year, Period, Entity and Value.
    /// </summary>
    public class PhasedSubmissionProcessFlow : ProcessFlow
    {

        protected override string ProcessUnitType { get { return "phased submission"; } }


        internal PhasedSubmissionProcessFlow(Session session)
            : base(session)
        { }


        protected override POV[] GetProcessUnits(Slice slice)
        {
            DefaultMembers(slice, true);
            return slice.POVs;
        }


        protected override void GetProcessState(Slice slice, IOutput output)
        {
            short state = 0;

            output.SetHeader("POV", 58, "Process State", 15);
            foreach(var pov in GetProcessUnits(slice)) {
                if(HFM.HasVariableCustoms) {
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


        protected override void GetHistory(POV pov, IOutput output)
        {
            object oDates = null, oUsers = null, oActions = null, oStates = null,
                   oAnnotations = null, oPaths = null, oFiles = null;

            if(HFM.HasVariableCustoms) {
                HFM.Try("Retrieving process history",
                        () => _hsvProcessFlow.PhasedSubmissionGetHistory2ExtDim(pov.HfmPovCOM,
                                        out oDates, out oUsers, out oActions, out oStates,
                                        out oAnnotations, out oPaths, out oFiles));
            }
            else {
                HFM.Try("Retrieving process history",
                        () => _hsvProcessFlow.PhasedSubmissionGetHistory2(pov.Scenario.Id, pov.Year.Id, pov.Period.Id,
                                        pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id, pov.Account.Id, pov.ICP.Id,
                                        pov.Custom1.Id, pov.Custom2.Id, pov.Custom3.Id, pov.Custom4.Id,
                                        out oDates, out oUsers, out oActions, out oStates,
                                        out oAnnotations, out oPaths, out oFiles));
            }
            OutputHistory(output, pov, oDates, oUsers, oActions, oStates,
                    oAnnotations, oPaths, oFiles);
        }


        protected override EProcessState SetProcessState(POV pov, EProcessAction action, EProcessState targetState,
                string annotation, string[] paths, string[] files)
        {
            short newState = 0;

            if(HFM.HasVariableCustoms) {
                HFM.Try("Setting phased submission state",
                        () => _hsvProcessFlow.PhasedSubmissionProcessManagementChangeStateForMultipleEntities2ExtDim(
                                    pov.HfmSliceCOM, annotation, (int)action, false, false,
                                    (short)targetState, paths, files, out newState));
            }
            else {
                HFM.Try("Setting phased submission state",
                        () => _hsvProcessFlow.PhasedSubmissionProcessManagementChangeStateForMultipleEntities2(
                                    pov.Scenario.Id, pov.Year.Id, pov.Period.Id, new int[] { pov.Entity.Id },
                                    new int[] { pov.Entity.ParentId }, pov.Value.Id, new int[] { pov.Account.Id },
                                    new int[] { pov.ICP.Id }, new int[] { pov.Custom1.Id }, new int[] { pov.Custom2.Id },
                                    new int[] { pov.Custom3.Id }, new int[] { pov.Custom4.Id },
                                    annotation, (int)action, false, false, (short)targetState,
                                    paths, files, out newState));
            }
            return (EProcessState)newState;
        }

    }

}
