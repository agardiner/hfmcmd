using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using HSXCLIENTLib;
using HSVSESSIONLib;

using Command;


namespace HFM
{

    /// <summary>
    /// The main entry point to the HFM object model. Represents the HFM
    /// functionality available on a machine with the HFM client installed.
    /// Note that the HsxClient COM object is represented by two classes:
    /// Client and Connection. This better models the fact that some methods
    /// can be called on a newly constructed HsxClient object without providing
    /// server connection details, while others need an authenticated user.
    /// </summary>
    public class Client
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsxClient COM object
        protected readonly HsxClient _client;


        // Constructor
        [Factory]
        public Client()
        {
            _log.Debug("Creating HsxClient instance");
            try {
                _client = new HsxClient();
            }
            catch(COMException ex) {
                _log.Error("Unable to instantiate an HsxClient COM object; is HFM installed?", ex);
            }
        }


        /// Sets the user credentials via userid and password.
        [Command, Factory,
         Description("Sets the connection details needed to communicate with an HFM server. " +
                     "User authentication is performed via Shared Services, so the login " +
                     "details need to be those of a Shared Services user (native or external) " +
                     "that has been provisioned to one or more HFM applications.")]
        public Connection SetLogonInfo(
                [Description("The Windows domain to which the user belongs"), DefaultValue(null)] string domain,
                [Description("The user name to login with") ] string userName,
                [Description("The password to login with"), SensitiveValue] string password)
        {
            _log.Debug("Setting logon credentials via username and password");
            _client.SetLogonInfoSSO(domain, userName, null, password);
            return new Connection(_client);
        }


        /// Sets the user credentials via an SSO token.
        [Command /*, Factory*/]
        public Connection SetLogonToken(
                [Description("An SSO token obtained from an existing Shared Services connection")] string token)
        {
            _log.Debug("Setting logon credentials via SSO token");
            _client.SetLogonInfoSSO(null, null, token, null);
            return new Connection(_client);
        }


        [Command]
        public void GetClusters()
        {
            object clusters;
            object servers;

            _client.GetClustersAndServers(out clusters, out servers);
            if(clusters != null) {
                _log.Info("Clusters:");
                foreach(var cluster in clusters as string[]) {
                    _log.InfoFormat("  {0}", cluster);
                }
            }
            if(servers != null) {
                _log.Info("Servers:");
                foreach(var server in servers as string[]) {
                    _log.InfoFormat("  {0}", server);
                }
            }
        }

    }


    /// <summary>
    /// This class wraps those methods on the HsxClient object that require
    /// login details.
    /// </summary>
    public class Connection
    {

        protected HsxClient _client;


        internal Connection(HsxClient client)
        {
            _client = client;
        }


        /// Opens the named application, and returns a Session object for
        /// interacting with it.
        [Command, Factory]
        public Session OpenApplication(string clusterName, string appName)
        {
            object hsxServer = null, hsvSession = null;
            _client.OpenApplication(clusterName, "Financial Management", appName,
                    out hsxServer, out hsvSession);
            return new Session((HsvSession)hsvSession);
        }


        /// Creates a new HFM application.
        [Command]
        public void CreateApplication(string clusterName, string appName,
                [DefaultValue("")] string appDesc, string profilePath,
                string sharedServicesProject, string appWebServerUrl)
        {
            byte[] profile = File.ReadAllBytes(profilePath);

            _client.CreateApplicationCAS(clusterName, "Financial Management",
                    appName, appDesc, "", profile, null, null, null, null,
                    sharedServicesProject, appWebServerUrl);
        }


        /// Deletes a classic (but not an EPMA) HFM application.
        [Command]
        public void DeleteApplication(string clusterName, string appName)
        {
            _client.DeleteApplication(clusterName, "Financial Management", appName);
        }
    }

}
