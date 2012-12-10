using System;

using log4net;
// We have to include the following lib even when using dynamic, since it contains
// the definition of the enums
using HSVSECURITYACCESSLib;

using Command;


namespace HFM
{

    /// <summary>
    /// Enumeration of tasks
    /// </summary>
    public enum ETask
    {
        ExtendedAnalytics = tagHFM_TASK_ENUM.HFM_TASK_EXTENDED_ANALYTICS,
        ProcessManagement = tagHFM_TASK_ENUM.HFM_TASK_DATA_EXPLORER_MANAGE_PROCESS
    }


    /// <summary>
    /// Enumeration of roles
    /// </summary>
    public enum ERole
    {
        Default = HFM_ROLE_ENUM.HFM_ROLE_DEFAULT,
        ProcessFlowReviewer1 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER1,
        ProcessFlowReviewer2 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER2,
        ProcessFlowReviewer3 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER3,
        ProcessFlowReviewer4 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER4,
        ProcessFlowReviewer5 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER5,
        ProcessFlowReviewer6 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER6,
        ProcessFlowReviewer7 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER7,
        ProcessFlowReviewer8 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER8,
        ProcessFlowReviewer9 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER9,
        ProcessFlowReviewer10 = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_REVIEWER10,
        ProcessFlowSubmitter = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_SUBMITTER,
        ProcessFlowSupervisor = HFM_ROLE_ENUM.HFM_ROLE_PROCESS_FLOW_SUPERVISOR
    }


    /// <summary>
    /// Enumeration of access levels
    /// </summary>
    public enum EAccessRights
    {
        None = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_NONE,
        Read = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_READONLY,
        Promote = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_READANDPROMOTE,
        Metadata = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_VIEW,
        All = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_ALL
    }



    public class AccessDeniedException : Exception
    {
        public AccessDeniedException(string msg) : base(msg) { }
    }



    /// <summary>
    /// </summary>
    public class Security
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to the Metadata module
        private Metadata _metadata;
        // Reference to the HsvSecurityAccess COM module
#if LATE_BIND
        internal readonly dynamic HsvSecurity;
        // Reference to the IHsvDataSecurity COM interface
        internal dynamic HsvDataSecurity {
            get {
                return HsvSecurity;
            }
        }
#else
        internal readonly HsvSecurityAccess HsvSecurity;
        // Reference to the IHsvDataSecurity COM interface
        internal IHsvDataSecurity HsvDataSecurity {
            get {
                return (IHsvDataSecurity)HsvSecurity;
            }
        }
#endif


        public Security(Session session)
        {
            _log.Trace("Constructing Security object");
#if LATE_BIND
            HsvSecurity = session.HsvSession.Security;
#else
            HsvSecurity = (HsvSecurityAccess)session.HsvSession.Security;
#endif
            _metadata = session.Metadata;
        }


        /// <summary>
        /// Returns true if the user has access sufficient to perform the specified task.
        /// </summary>
        public void CheckPermissionFor(ETask task)
        {
            bool allowed = false;

            HFM.Try("Checking task permission",
                    () => HsvSecurity.IsConnectedUserAllowedToPerformTask((int)task, out allowed));
            if(!allowed) {
                throw new AccessDeniedException(string.Format("You do not have permission to perform {0}", task));
            }
        }


        /// <summary>
        /// Returns true if the user has the specified role assigned (directly
        /// or indirectly through group membership).
        /// </summary>
        public void CheckRole(ERole role)
        {
            bool allowed = false;

            HFM.Try("Checking role permission",
                    () => HsvSecurity.IsConnectedUserInRole((int)role, out allowed));
            if(!allowed) {
                throw new AccessDeniedException(string.Format("You have not been assigned the role {0}", role));
            }
        }


        /// <summary>
        /// Returns the current user's access rights to the specified cell
        /// </summary>
        public EAccessRights GetCellLevelAccessRights(POV pov)
        {
            int accessRights = 0;
            if(HFM.HasVariableCustoms) {
#if HFM_11_1_2_2
                HFM.Try("Retrieving cell access rights",
                        () => HsvDataSecurity.GetCellLevelAccessRightsExtDim(pov.HfmPovCOM,
                                                                             out accessRights));
#else
                HFM.ThrowIncompatibleLibraryEx();
#endif
            }
            else {
                HFM.Try("Retrieving cell access rights",
                        () => HsvDataSecurity.GetCellLevelAccessRights(pov.Scenario.Id, pov.Year.Id,
                                     pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                     pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                     pov.Custom3.Id, pov.Custom4.Id, out accessRights));
            }
            return (EAccessRights)accessRights;
        }


        /// <summary>
        /// Returns the current user's access rights to the specified process unit
        /// </summary>
        public EAccessRights GetProcessUnitAccessRights(POV pov, out EProcessState state)
        {
            int accessRights = 0;
            short currentState = 0;
            if(_metadata.UsesPhasedSubmissions) {
                if(HFM.HasVariableCustoms) {
#if HFM_11_1_2_2
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsAndStateExExtDim(pov.HfmPovCOM,
                                        Member.NOT_USED, out accessRights, out currentState));
#else
                    HFM.ThrowIncompatibleLibraryEx();
#endif
                }
                else {
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsAndStateEx(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                        pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                        pov.Custom3.Id, pov.Custom4.Id, Member.NOT_USED,
                                        out accessRights, out currentState));
                }
            }
            else {
                HFM.Try("Retrieving process unit access rights",
                        () => HsvDataSecurity.GetProcessUnitAccessRightsAndState(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                        out accessRights, out currentState));
            }
            state = (EProcessState)currentState;
            return (EAccessRights)accessRights;
        }

    }

}
