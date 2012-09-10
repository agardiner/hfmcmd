using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using HSXCLIENTLib;
using HSVSESSIONLib;

using Command;


namespace HFM
{

    // TODO: Add following commands:
    // - CreateObjectOnCluster
    // - DeleteSystemErrors
    // - DisableNewConnections
    // - EnableNewConnections
    // - EnumProhibitConnections
    // - EnumProvisioningProjects
    // - EnumRegisteredClusterNames
    // - EnumUsersOnSystem
    // - EnumUsersOnSystemEx
    // - EnumUsersOnSystemEx2
    // - GetApplicationFolder
    // - GetClusterInfo
    // - GetServerOnCluster
    // - IsValidApplication
    // - KillUsers
    // - RegisterApplicationCAS
    // - RegisterCluster
    // - SetApplicationFolder
    // - UnregisterAllClusters
    // - UnregisterCluster

    // TODO: Determine if the following ought to be exposed:
    // - EnumUserAppPreferences
    // - UpdateUserAppPreferences
    // - WarnUsersForShutDown

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
            HFM.Try("Setting logon credentials via username and password",
                    () => _client.SetLogonInfoSSO(domain, userName, null, password));
            return new Connection(_client);
        }


        /// Sets the user credentials via an SSO token.
        [Command, AlternateFactory,
         Description("Sets the connection details using an SSO token. An SSO token represents " +
                     "an existing authenticated session, and may been obtained from the GetLogonToken " +
                     "command, or from another Hyperion session.")]
        public Connection SetLogonToken(
                [Description("An SSO token obtained from an existing Shared Services connection")] string token)
        {
            HFM.Try("Setting logon credentials via SSO token",
                    () => _client.SetLogonInfoSSO(null, null, token, null));
            return new Connection(_client);
        }


        [Command, Description("Returns the names of the HFM clusters and/or servers registered on this machine")]
        public void GetClusters(IOutput output)
        {
            object clusters = null;
            object servers = null;

            HFM.Try("Retrieving names of registered clusters / servers",
                    () => _client.GetClustersAndServers(out clusters, out servers));
            if(clusters != null) {
                output.SetFields("Cluster");
                foreach(var cluster in clusters as string[]) {
                    output.WriteRecord(cluster);
                }
            }
        }


        [Command]
        public string GetWindowsLoggedOnUser(IOutput output)
        {
            string domain = null, userid = null;
            HFM.Try("Determining current logged in Windows user",
                    () => _client.DetermineWindowsLoggedOnUser(out domain, out userid));
            output.SetFields("Domain", "User Name");
            output.WriteRecord(domain, userid);
            return string.Format(@"{0}\{1}", domain, userid);
        }


        public void DeleteSystemErrors(string clusterName,
                [DefaultValue(true)] bool deleteAll,
                [DefaultValue(null)] string[] errorReferences)
        {

        }


    }


    /// <summary>
    /// This class wraps those methods on the HsxClient object that require
    /// login details.
    /// </summary>
    public class Connection
    {

        protected HsxClient _client;
        protected string _app;


        internal Connection(HsxClient client)
        {
            _client = client;
        }


        [Command, Description("Returns the domain, user name, and Single Sign-On (SSO) token " +
                              "for the currently authenticated user. Note that an SSO token is " +
                              "only returned however, once an application has been opened. " +
                              "If called with no application open, the domain and user id will " +
                              "be returned, but not an SSO token.")]
        public string GetLogonToken(IOutput output)
        {
            string domain, user;

            var token = _client.GetLogonInfoSSO(out domain, out user);
            output.SetFields("Domain", "UserName", "SSO Token");
            output.WriteRecord(domain, user, token);
            return token;
        }


        /// Opens the named application, and returns a Session object for
        /// interacting with it.
        [Command, Factory,
         Description("Open an HFM application and establish a session.")]
        public Session OpenApplication(string clusterName, string appName)
        {
            object hsxServer = null, hsvSession = null;
            HFM.Try(string.Format("Opening application {0} on {1}", appName, clusterName),
                    () => _client.OpenApplication(clusterName, "Financial Management", appName,
                            out hsxServer, out hsvSession));
            _app = appName;
            return new Session((HsvSession)hsvSession);
        }


        [Command, Description("Creates a new HFM classic application.")]
        public void CreateApplication(string clusterName, string appName,
                string appDesc, string profilePath,
                [DefaultValue("Default Application Group")] string sharedServicesProject,
                [Description("The URL of the virtual directory for Financial Management. " +
                 "The URL should include the protocol, Web server name and port, and virtual " +
                 "directory name.")] string appWebServerUrl)
        {
            byte[] profile = File.ReadAllBytes(profilePath);

            HFM.Try(string.Format("Creating application {0} on {1}", appName, clusterName),
                    () => _client.CreateApplicationCAS(clusterName, "Financial Management",
                            appName, appDesc, "", profile, null, null, null, null,
                            sharedServicesProject, appWebServerUrl));
        }


        [Command,
         Description("Deletes the specified HFM (classic) application. Note: HFM applications " +
                     "created via or migrated to EPMA cannot be deleted via this command.")]
        public void DeleteApplication(string clusterName, string appName)
        {
            HFM.Try(string.Format("Deleting application {0} on {1}", appName, clusterName),
                    () => _client.DeleteApplication(clusterName, "Financial Management", appName));
        }


        [Command]
        public bool CanCreateApplications(string clusterName, IOutput output)
        {
            bool hasAccess = false;
            HFM.Try("Checking if user has CreateApplication rights",
                    () => _client.DoesUserHaveCreateApplicationRights(clusterName, out hasAccess));
            output.SetFields("Cluster", "CreateApplication");
            output.WriteRecord(clusterName, hasAccess ? "true" : "false");
            return hasAccess;
        }


        [Command]
        public bool UserHasSystemAdminRights(string clusterName, IOutput output)
        {
            bool hasAdmin = false;
            HFM.Try("Checking if user has SystemAdmin rights",
                    () => _client.DoesUserHaveSystemAdminRights(clusterName, out hasAdmin));
            output.SetFields("Cluster", "SystemAdmin");
            output.WriteRecord(clusterName, hasAdmin ? "true" : "false");
            return hasAdmin;
        }
    }

}
