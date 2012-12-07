using System;
using System.IO;

using log4net;

// We have to include the following lib even when using dynamic, since it contains
// the definition of the enums
using HSVSECURITYLOADACVLib;

using Command;
using HFMCmd;


namespace HFM
{

    public class SecurityLoad
    {

        /// <summary>
        /// Collection class holding options that can be specified when loading
        /// security settings.
        /// </summary>
        [Setting("ValidateUsers", "Validate User and Group names"),
         Setting("ClearAll", "Clear existing security information before load"),
         Setting("Delimiter", "Security file delimiter",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("RoleAccess", "Load Role Access definitions"),
         Setting("SecurityClassAccess", "Load Security Class Access definitions"),
         Setting("SecurityClasses", "Load Security Class definitions"),
         Setting("Users", "Load User and Group names")]
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(SecurityLoad sl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_SECURITYLOAD_OPTION),
                     (IHsvLoadExtractOptions)sl.HsvSecurityLoad.LoadOptions)
            {
            }
        }


        /// <summary>
        /// Collection class holding options that can be specified when extracting
        /// security.
        /// </summary>
        [Setting("Delimiter", "File delimiter used in security extract file",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("RoleAccess", "Extract Role Access definitions"),
         Setting("SecurityClassAccess", "Extract Security Class Access definitions"),
         Setting("SecurityClasses", "Extract Security Class definitions"),
         Setting("Users", "Extract Users and Groups")]
        public class ExtractOptions : LoadExtractOptions
        {
            [Factory]
            public ExtractOptions(SecurityLoad sl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_SECURITYEXTRACT_OPTION),
                     (IHsvLoadExtractOptions)sl.HsvSecurityLoad.ExtractOptions)
            {
            }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvSecurityLoadACV object
#if LATE_BIND
        internal readonly dynamic HsvSecurityLoad;
#else
        internal readonly HsvSecurityLoadACV HsvSecurityLoad;
#endif


        [Factory]
        public SecurityLoad(Session session)
        {
            _log.Trace("Constructing SecurityLoad object");
#if LATE_BIND
            HsvSecurityLoad = new HsvSecurityLoadACV();
#else
            HsvSecurityLoad = new HsvSecurityLoadACV();
#endif
            HsvSecurityLoad.SetSession(session.HsvSession);
        }


        [Command("Loads an HFM application's security from a native ASCII or XML file")]
        public void LoadSecurity(
                [Parameter("Path to the source security extract file")]
                string securityFile,
                [Parameter("Path to the load log file; if not specified, defaults to same path " +
                             "and name as the source security file.",
                 DefaultValue = null)]
                string logFile,
                LoadOptions options)
        {
            object oWarnings = null;

            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(securityFile, ".log");
            }

            // Ensure security file exists and logFile is writeable
            Utilities.EnsureFileExists(securityFile);
            Utilities.EnsureFileWriteable(logFile);

            HFM.Try("Loading security",
                    () => HsvSecurityLoad.Load(securityFile, logFile, out oWarnings));
            if((bool)oWarnings) {
                _log.Warn("Security load resulted in warnings; check log file for details");
                // TODO:  Should we show the warnings here?
            }
        }


        [Command("Extracts an HFM application's security to a native ASCII or XML file")]
        public void ExtractSecurity(
                [Parameter("Path to the generated security extract file")]
                string securityFile,
                [Parameter("Path to the extract log file; if not specified, defaults to same path " +
                           "and name as extract file.", DefaultValue = null)]
                string logFile,
                ExtractOptions options)
        {
            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(securityFile, ".log");
            }
            // TODO: Display options etc
            _log.FineFormat("    Security file: {0}", securityFile);
            _log.FineFormat("    Log file:     {0}", logFile);

            // Ensure securityFile and logFile are writeable locations
            Utilities.EnsureFileWriteable(securityFile);
            Utilities.EnsureFileWriteable(logFile);

            HFM.Try("Extracting security",
                    () => HsvSecurityLoad.Extract(securityFile, logFile));
        }

    }

}
