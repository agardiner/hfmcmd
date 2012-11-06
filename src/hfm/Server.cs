using System;

using log4net;
using HSXCLIENTLib;
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

        // Reference to HsxClient object
        protected HsxClient _hsxClient;

        // Name of the last cluster accessed
        protected string _cluster;
        // Reference to current HsxServer object; these are cached
        protected HsxServer _hsxServer;


        [Factory]
        public Server(Client client)
        {
            _hsxClient = client.HsxClient;
        }


        private void SetCluster(string cluster)
        {
            if(_cluster != cluster) {
                object server = null;
                HFM.Try("Retrieving HsxServer instance",
                        () => server = _hsxClient.GetServerOnCluster(cluster));
                _hsxServer = (HsxServer)server;
                _cluster = cluster;
            }
        }


        [Command("Returns the names of the applications that are known to the server")]
        public string[] EnumApplications(
                [Parameter("The name of the cluster or server whose applications are to be returned")]
                string clusterName,
                IOutput output)
        {
            object products, apps = null, descs = null, dsns;
            string[] sApps, sDescs;

            SetCluster(clusterName);
            HFM.Try("Retrieving names of applications",
                    () => _hsxServer.EnumDataSources(out products, out apps, out descs, out dsns));
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
                [Parameter("The name of the cluster or server whose applications are to be returned")]
                string clusterName,
                IOutput output)
        {
            string[] dsns = null;

            SetCluster(clusterName);
            HFM.Try("Retrieving names of DSNs",
                    () => dsns = _hsxServer.EnumRegisteredDSNs() as string[]);

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
                [Parameter("The name of the cluster or server whose applications are to be returned")]
                string clusterName,
                IOutput output)
        {
            string folder = null;

            SetCluster(clusterName);
            HFM.Try("Retrieving system folder",
                    () => folder = _hsxServer.GetSystemFolder());
            output.WriteSingleValue(folder, "System Folder");
            return folder;
        }


        [Command("Determines whether the specified application is a classic or EPMA application")]
        public bool IsClassicHFMApplication(
                [Parameter("The name of the cluster on which the application exists")]
                string clusterName,
                [Parameter("The name of the application")]
                string appName,
                IOutput output)
        {
            bool isClassic = true;
            SetCluster(clusterName);
            HFM.Try("Checking application",
                    () => isClassic = ((IHsxServerInternal)_hsxServer).IsClassicHFMApplication(appName));
            output.SetHeader("Cluster", "Application", "Type", 10);
            output.WriteSingleRecord(clusterName, appName, isClassic ? "Classic" : "EPMA");
            return isClassic;
        }

    }
}
