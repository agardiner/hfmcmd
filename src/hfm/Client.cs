using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using HSXCLIENTLib;
using HSXSERVERLib;
using HSVSESSIONLib;

using Command;
using HFMCmd;


namespace HFM
{

    // TODO: Add following commands:
    // - CreateObjectOnCluster
    // - DeleteSystemErrors
    // - DisableNewConnections
    // - EnableNewConnections
    // - EnumProhibitConnections
    // - EnumUsersOnSystem
    // - EnumUsersOnSystemEx
    // - EnumUsersOnSystemEx2
    // - GetApplicationFolder
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
                unchecked {
                    if(ex.ErrorCode == (int)0x80040154) {
                        _log.Error("Unable to instantiate an HsxClient COM object; " +
                                   "is HFM installed on this machine?", ex);
                    }
                }
                throw ex;
            }
        }


        /// Sets the user credentials via userid and password.
        [Command("Sets the connection details needed to communicate with an HFM server. " +
                 "User authentication is performed via Shared Services, so the login " +
                 "details need to be those of a Shared Services user (native or external) " +
                 "that has been provisioned to one or more HFM applications."),
         Factory]
        public Connection SetLogonInfo(
                [Parameter("The Windows domain to which the user belongs", DefaultValue = null)]
                string domain,
                [Parameter("The user name to login with")]
                string userName,
                [Parameter("The password to login with", IsSensitive = true)]
                string password)
        {
            HFM.Try("Setting logon credentials via username and password",
                    () => _client.SetLogonInfoSSO(domain, userName, null, password));
            return new Connection(_client);
        }


        /// Sets the user credentials via an SSO token.
        [Command("Sets the connection details using an SSO token. An SSO token represents " +
                 "an existing authenticated session, and may been obtained from the GetLogonToken " +
                 "command, or from another Hyperion session."),
         AlternateFactory]
        public Connection SetLogonToken(
                [Parameter("An SSO token obtained from an existing Shared Services connection")]
                string token)
        {
            HFM.Try("Setting logon credentials via SSO token",
                    () => _client.SetLogonInfoSSO(null, null, token, null));
            return new Connection(_client);
        }


        [Factory]
        public Server GetServer(string clusterName)
        {
            object server = null;
            HFM.Try("Retrieving HsxServer instance",
                    () => server = _client.GetServerOnCluster(clusterName));
            return new Server((HsxServer)server);
        }


        [Command("Returns the names of the HFM clusters and/or servers registered on this machine")]
        public string[] GetClusters(IOutput output)
        {
            object clusters = null;

            HFM.Try("Retrieving names of registered clusters / servers",
                    () => clusters = _client.EnumRegisteredClusterNames());
            if(clusters != null) {
                output.SetFields("Cluster");
                foreach(var cluster in clusters as string[]) {
                    output.WriteRecord(cluster);
                }
            }
            return clusters as string[];
        }


        // TODO: This method should probably return something
        [Command("Outputs cluster info")]
        public void GetClusterInfo(
                [Parameter("The name of the server")]
                string serverName,
                [Parameter("True to return the cluster name for the server, false to return the server name")]
                bool loadBalanced,
                IOutput output)
        {
            string clusterName = null;
            // TODO: This seems to always throw an exception!?
            HFM.Try("Obtaining cluster information for server",
                    () => _client.GetClusterInfo(serverName, loadBalanced, out clusterName));
            if(clusterName != null) {
                output.SetFields("Cluster Name");
                output.WriteRecord(clusterName as string);
            }
        }


        [Command("Returns the domain and user name of the currently logged in Windows user")]
        public string GetWindowsLoggedOnUser(IOutput output)
        {
            string domain = null, userid = null;
            HFM.Try("Determining current logged on Windows user",
                    () => _client.DetermineWindowsLoggedOnUser(out domain, out userid));
            output.SetFields("Domain", "User Name");
            output.WriteRecord(domain, userid);
            return string.Format(@"{0}\{1}", domain, userid);
        }


        [Command("Deletes system error messages")]
        public void DeleteSystemErrors(
                [Parameter("Name of the cluster on which to delete system errors")]
                string clusterName,
                [Parameter("List of error numbers to delete", DefaultValue = null)]
                string[] errorReferences)
        {
            // TODO: Implement me!
        }


    }


    /// <summary>
    /// This class wraps those methods on the HsxClient object that require
    /// login details.
    /// </summary>
    public class Connection
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        protected HsxClient _client;
        protected string _app;


        internal Connection(HsxClient client)
        {
            _client = client;
        }


        [Command("Returns the domain, user name, and Single Sign-On (SSO) token " +
                 "for the currently authenticated user. Note that an SSO token is " +
                 "only returned however, once an application has been opened. " +
                 "If called with no application open, the domain and user id will " +
                 "be returned, but not an SSO token.")]
        public string GetLogonToken(IOutput output)
        {
            string domain = null, user = null, token = null;

            HFM.Try("Retrieving logon info",
                () => token = _client.GetLogonInfoSSO(out domain, out user));
            if(_app == null) {
                _log.Warn("SSO token cannot be retrieved until an application has been opened");
            }
            output.SetFields("Domain", "UserName", "SSO Token");
            output.WriteRecord(domain, user, token);
            return token;
        }


        /// Opens the named application, and returns a Session object for
        /// interacting with it.
        [Factory,
         Command("Open an HFM application and establish a session.")]
        public Session OpenApplication(
                [Parameter("The name of the cluster on which to open the application")]
                string clusterName,
                [Parameter("The name of the application to open")]
                string appName)
        {
            object hsxServer = null, hsvSession = null;
            HFM.Try(string.Format("Opening application {0} on {1}", appName, clusterName),
                    () => _client.OpenApplication(clusterName, "Financial Management", appName,
                            out hsxServer, out hsvSession));
            _app = appName;
            return new Session((HsvSession)hsvSession);
        }


        [Command("Returns the names of the available provisioning projects in Shared Services.")]
        public string[] GetProvisioningProjects(
                [Parameter("The name of the cluster from which to obtain the Shared Services information")]
                string clusterName,
                IOutput output)
        {
            object projects = null;
            HFM.Try("Retrieving names of provisioning projects",
                    () => projects = _client.EnumProvisioningProjects(clusterName));
            if(projects != null) {
                output.SetFields("Project");
                foreach(var project in projects as string[]) {
                    output.WriteRecord(project);
                }
            }
            return projects as string[];
        }


        [Command("Creates a new HFM classic application.")]
        public void CreateApplication(
                [Parameter("The name of the cluster on which to create the application")]
                string clusterName,
                [Parameter("The name to be given to the new application")]
                string appName,
                [Parameter("The description for the new application (cannot be blank)")]
                string appDesc,
                [Parameter("Path to the application profile (.per) file used to define the " +
                           "time and custom dimensions")]
                string profilePath,
                [Parameter("The name of the project to assign the application to in Shared Services",
                 DefaultValue = "Default Application Group")]
                string sharedServicesProject,
                [Parameter("The URL of the virtual directory for Financial Management. " +
                 "The URL should include the protocol, Web server name and port, and virtual " +
                 "directory name, e.g. http://<server>:80/hfm")]
                string appWebServerUrl)
        {
            byte[] profile = File.ReadAllBytes(profilePath);

            HFM.Try(string.Format("Creating application {0} on {1}", appName, clusterName),
                    () => _client.CreateApplicationCAS(clusterName, "Financial Management",
                            appName, appDesc, "", profile, null, null, null, null,
                            sharedServicesProject, appWebServerUrl));
        }


        [Command("Deletes the specified HFM (classic) application. Note: HFM applications " +
                 "created via or migrated to EPMA cannot be deleted via this command.")]
        public void DeleteApplication(
                [Parameter("The name of the cluster from which to delete the application")]
                string clusterName,
                [Parameter("The name of the application to be deleted")]
                string appName)
        {
            HFM.Try(string.Format("Deleting application {0} on {1}", appName, clusterName),
                    () => _client.DeleteApplication(clusterName, "Financial Management", appName));
        }


        [Command("Returns true if the connected user has CreateApplication rights on the HFM cluster")]
        public bool UserHasCreateApplicationRights(
                [Parameter("The name of the cluster on which to check the user rights")]
                string clusterName,
                IOutput output)
        {
            bool hasAccess = false;
            HFM.Try("Checking if user has CreateApplication rights",
                    () => _client.DoesUserHaveCreateApplicationRights(clusterName, out hasAccess));
            output.SetFields("Cluster", "CreateApplication");
            output.WriteRecord(clusterName, hasAccess ? "true" : "false");
            return hasAccess;
        }


        [Command("Returns true if the connected user has SystemAdmin rights on the HFM cluster")]
        public bool UserHasSystemAdminRights(
                [Parameter("The name of the cluster on which to check the user rights")]
                string clusterName,
                IOutput output)
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
