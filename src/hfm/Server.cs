using System;

using log4net;
using HSXSERVERLib;

using Command;
using HFMCmd;


namespace HFM
{

    // TODO: Determine what to do about CSSEnabled property
    // TODO: Define commands for:
    // - GetClustersAndServers
    // - GetSystemDataLinkFile
    // - GetSystemFolder
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


        // Reference to HFM HsxServer object
        protected readonly HsxServer _hsxServer;

        // Property for accessing underlying HsxServer instance; accessible only
        // to other HFM components
        internal HsxServer HsxServer { get { return _hsxServer; } }


        public Server(HsxServer server)
        {
            _hsxServer = server;
        }


        [Command("Returns the names of the applications that are known to the server")]
        public string[] GetApplications(IOutput output)
        {
            object products, apps = null, descs = null, dsns;
            string[] sApps, sDescs;
            HFM.Try("Retrieving names of applications",
                    () => _hsxServer.EnumDataSources(out products, out apps, out descs, out dsns));
            sApps = apps as string[];
            sDescs = descs as string[];
            output.SetFields("Application", "Description");
            for(var i = 0; i < sApps.Length; ++i) {
                output.WriteRecord(sApps[i], sDescs[i]);
            }
            return sApps;
        }


        [Command("Returns the names of the Extended Analytics DSNs that are registered on the server")]
        public string[] GetDSNs(IOutput output)
        {
            object dsns = null;
            string[] sDSNs;
            HFM.Try("Retrieving names of DSNs",
                    () => dsns = _hsxServer.EnumRegisteredDSNs());
            sDSNs = dsns as string[];
            output.SetFields("DSN Name");
            if(sDSNs == null) {
                sDSNs = new string[] {};
            }
            foreach(var dsn in sDSNs) {
                output.WriteRecord(dsn);
            }
            return sDSNs;
        }

    }
}
