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
            if(output != null) {
                output.SetHeader("Application", "Description", 50);
                for(var i = 0; i < sApps.Length; ++i) {
                    output.WriteRecord(sApps[i], sDescs[i]);
                }
                output.End();
            }
            return sApps;
        }


        [Command("Returns the names of the Extended Analytics DSNs that are registered on the server")]
        public string[] GetDSNs(IOutput output)
        {
            string[] dsns = null;

            HFM.Try("Retrieving names of DSNs",
                    () => dsns = _hsxServer.EnumRegisteredDSNs() as string[]);

            if(output != null && dsns != null) {
                output.WriteEnumerable(dsns, "DSN Name");
            }
            return dsns;
        }

    }
}
