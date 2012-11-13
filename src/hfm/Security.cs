using System;

using log4net;
using HSVSECURITYACCESSLib;

using Command;


namespace HFM
{

    /// <summary>
    /// Enumeration of tasks
    /// </summary>
    public enum ETask
    {
        ProcessManagement = tagHFM_TASK_ENUM.HFM_TASK_DATA_EXPLORER_MANAGE_PROCESS
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
        private HsvSecurityAccess _hsvSecurity;
        // Reference to the IHsvDataSecurity COM interface
        private IHsvDataSecurity HsvDataSecurity {
            get {
                return (IHsvDataSecurity)_hsvSecurity;
            }
        }



        public Security(Session session)
        {
            _hsvSecurity = (HsvSecurityAccess)session.HsvSession.Security;
            _metadata = session.Metadata;
        }


        /// <summary>
        /// Returns true if the user has access sufficient to perform the specified task.
        /// </summary>
        public void CheckPermissionFor(ETask task)
        {
            bool allowed = false;

            HFM.Try("Checking task permission",
                    () => _hsvSecurity.IsConnectedUserAllowedToPerformTask((int)task, out allowed));
            if(!allowed) {
                throw new AccessDeniedException(string.Format("You do not have permission to perform {0}", task));
            }
        }


        /// <summary>
        /// Returns the current user's access rights to the specified cell
        /// </summary>
        public EAccessRights GetCellLevelAccessRights(POV pov)
        {
            int accessRights = 0;
            if(HFM.HasVariableCustoms) {
                HFM.Try("Retrieving cell access rights",
                        () => HsvDataSecurity.GetCellLevelAccessRightsExtDim(pov.HfmPovCOM,
                                                                             out accessRights));
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
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsAndStateExExtDim(pov.HfmPovCOM,
                                        -1, out accessRights, out currentState));
                }
                else {
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsAndStateEx(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                        pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                        pov.Custom3.Id, pov.Custom4.Id, -1, out accessRights, out currentState));
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
