using System;

using log4net;
#if !LATE_BIND
using HSXSERVERLib;
using HSXSERVERFILETRANSFERLib;
#endif

using Command;
using HFMCmd;


namespace HFM
{

    // TODO: Determine what to do about CSSEnabled property
    // TODO: Define commands for:
    // - GetClustersAndServers
    // - GetSystemDataLinkFile
    // - GetXMLErrorFromDatabase
    // - GetXMLErrorsListFromDatabase

    /// <summary>
    /// Represents an HFM application server.
    /// </summary>
    public class Server
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to the HsxServer object
#if LATE_BIND
        internal readonly dynamic HsxServer;
#else
        internal readonly HsxServer HsxServer;
#endif
        // Reference to a FileTransfer object
        protected FileTransfer _fileTransfer;


#if LATE_BIND
        public Server(dynamic hsxServer)
#else
        public Server(HsxServer hsxServer)
#endif
        {
            _log.Trace("Constructing Server object");
            HsxServer = hsxServer;
        }


        internal FileTransfer FileTransfer
        {
            get {
                if(_fileTransfer == null) {
                    _fileTransfer = new FileTransfer(this);
                }
                return _fileTransfer;
            }
        }


        [Command("Returns the names of the applications that are known to the server")]
        public string[] EnumApplications(
                IOutput output)
        {
            object products, apps = null, descs = null, dsns;
            string[] sApps, sDescs;

            HFM.Try("Retrieving names of applications",
                    () => HsxServer.EnumDataSources(out products, out apps, out descs, out dsns));
            sApps = apps as string[];
            sDescs = descs as string[];
            output.SetHeader("Application", "Description", 50);
            for(var i = 0; i < sApps.Length; ++i) {
                output.WriteRecord(sApps[i], sDescs[i]);
            }
            output.End();
            return sApps;
        }


        [Command("Returns the names of the Extended Analytics DSNs that are registered on the server")]
        public string[] EnumDSNs(
                IOutput output)
        {
            string[] dsns = null;

            HFM.Try("Retrieving names of DSNs",
                    () => dsns = HsxServer.EnumRegisteredDSNs() as string[]);

            if(dsns != null) {
                output.WriteEnumerable(dsns, "DSN Name");
            }
            else {
                output.WriteSingleValue("There are no DSNs registered on this server");
            }
            return dsns;
        }


        [Command("Returns the path to the HFM system folder on the server")]
        public string GetSystemFolder(
                IOutput output)
        {
            string folder = null;

            HFM.Try("Retrieving system folder",
                    () => folder = HsxServer.GetSystemFolder());
            output.WriteSingleValue(folder, "System Folder");
            return folder;
        }


        [Command("Determines whether the specified application is a classic or EPMA application")]
        public bool IsClassicHFMApplication(
                [Parameter("The name of the application",
                           Alias = "AppName")]
                string application,
                IOutput output)
        {
            bool isClassic = true;

            HFM.Try("Checking application",
#if LATE_BIND
                    () => isClassic = HsxServer.IsClassicHFMApplication(application));
#else
                    () => isClassic = ((IHsxServerInternal)HsxServer).IsClassicHFMApplication(application));
#endif
            output.SetHeader("Application", "Type", 10);
            output.WriteSingleRecord(application, isClassic ? "Classic" : "EPMA");
            return isClassic;
        }

    }
}
