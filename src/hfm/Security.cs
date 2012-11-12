using System;

using log4net;
using HSVSECURITYACCESSLib;

using Command;


namespace HFM
{

    public enum EAccessRights
    {
        None = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_NONE,
        Read = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_READONLY,
        Promote = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_READANDPROMOTE,
        Metadata = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_VIEW,
        All = HFM_NUM_ACCESS_TYPES.HFM_ACCESS_RIGHTS_ALL
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



        [Factory]
        public Security(Session session)
        {
            _hsvSecurity = (HsvSecurityAccess)session.HsvSession.Security;
            _metadata = session.Metadata;
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
        public EAccessRights GetProcessUnitAccessRights(POV pov)
        {
            int accessRights = 0;
            if(_metadata.UsesPhasedSubmissions) {
                if(HFM.HasVariableCustoms) {
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsExExtDim(pov.HfmPovCOM,
                                        -1, out accessRights));
                }
                else {
                    HFM.Try("Retrieving phased submission access rights",
                            () => HsvDataSecurity.GetProcessUnitAccessRightsEx(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                        pov.Account.Id, pov.ICP.Id, pov.Custom1.Id, pov.Custom2.Id,
                                        pov.Custom3.Id, pov.Custom4.Id, -1, out accessRights));
                }
            }
            else {
                HFM.Try("Retrieving process unit access rights",
                        () => HsvDataSecurity.GetProcessUnitAccessRights(pov.Scenario.Id, pov.Year.Id,
                                        pov.Period.Id, pov.Entity.Id, pov.Entity.ParentId, pov.Value.Id,
                                        out accessRights));
            }
            return (EAccessRights)accessRights;
        }

    }

}
