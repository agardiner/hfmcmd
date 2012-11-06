using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using HSXCLIENTLib;
using HSXSERVERLib;
using HFMWAPPLICATIONSLib;

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
        private readonly HsxClient _hsxClient;

        /// Property for returning the HsxClient COM object
        internal HsxClient HsxClient { get { return _hsxClient; } }


        // Constructor
        [Factory]
        public Client()
        {
            _log.Trace("Creating HsxClient instance");
            try {
                _hsxClient = new HsxClient();
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
            return new Connection(_hsxClient, domain, userName, password);
        }


        /// Sets the user credentials via an SSO token.
        [Command("Sets the connection details using an SSO token. An SSO token represents " +
                 "an existing authenticated session, and may been obtained from the GetLogonToken " +
                 "command, or from another Hyperion session."),
         Factory(Alternate = true)]
        public Connection SetLogonToken(
                [Parameter("An SSO token obtained from an existing Shared Services connection")]
                string token)
        {
            return new Connection(_hsxClient, token);
        }


        [Command("Returns the names of the HFM clusters and/or servers registered on this machine")]
        public string[] EnumClusters(IOutput output)
        {
            string[] clusters = null;

            HFM.Try("Retrieving names of registered clusters / servers",
                    () => clusters = _hsxClient.EnumRegisteredClusterNames() as string[]);
            if(clusters != null) {
                output.SetHeader("Cluster");
                foreach(var cluster in clusters) {
                    output.WriteRecord(cluster);
                }
                output.End();
            }
            else {
                output.WriteSingleValue("There are no clusters registered on this machine");
            }
            return clusters;
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
                    () => _hsxClient.GetClusterInfo(serverName, loadBalanced, out clusterName));
            if(clusterName != null) {
                output.WriteSingleValue(clusterName, "Cluster Name");
            }
        }


        [Command("Returns the domain and user name of the currently logged in Windows user")]
        public string GetWindowsLoggedOnUser(IOutput output)
        {
            string domain = null, userid = null;
            HFM.Try("Determining current logged on Windows user",
                    () => _hsxClient.DetermineWindowsLoggedOnUser(out domain, out userid));
            if(output != null) {
                output.SetHeader("Domain", "User Name", 30);
                output.WriteSingleRecord(domain, userid);
            }
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


        private HsxClient _hsxClient;
        private HFMwManageApplications _hfmwManageApps;

        private string _domain;
        private string _userName;
        private string _password;
        private string _token;
        protected bool _appOpened;

        internal HsxClient HsxClient { get { return _hsxClient; } }
        internal HFMwManageApplications HFMwManageApplications
        {
            get {
                if(_hfmwManageApps == null) {
                    _log.Trace("Creating HFMwManageApplications instance");
                    _hfmwManageApps = new HFMwManageApplications();
                    HFM.Try("Setting logon info for web application",
                            () => _hfmwManageApps.SetLogonInfoSSO(_domain, _userName,
                                                                  _token, _password));
                }
                return _hfmwManageApps;
            }
        }



        internal Connection(HsxClient client, string domain, string userName, string password)
        {
            _hsxClient = client;
            _domain = domain;
            _userName = userName;
            _password = password;
            HFM.Try("Setting logon credentials via username and password",
                    () => _hsxClient.SetLogonInfoSSO(domain, userName, null, password));
        }


        internal Connection(HsxClient client, string token)
        {
            _hsxClient = client;
            _token = token;
            HFM.Try("Setting logon credentials via SSO token",
                    () => _hsxClient.SetLogonInfoSSO(null, null, token, null));
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
                () => token = _hsxClient.GetLogonInfoSSO(out domain, out user));
            if(!_appOpened) {
                _log.Warn("SSO token cannot be retrieved until an application has been opened");
            }
            if(output != null) {
                output.SetHeader("Domain", "UserName", "SSO Token");
                output.WriteSingleRecord(domain, user, token);
            }
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
            _appOpened = true;
            return new Session(this, clusterName, appName);
        }


        [Command("Returns the names of the available provisioning projects in Shared Services.")]
        public string[] EnumProvisioningProjects(
                [Parameter("The name of the cluster from which to obtain the Shared Services information")]
                string clusterName,
                IOutput output)
        {
            string[] projects = null;
            HFM.Try("Retrieving names of provisioning projects",
                    () => projects = _hsxClient.EnumProvisioningProjects(clusterName) as string[]);
            if(output != null && projects != null) {
                output.WriteEnumerable(projects, "Project", 40);
            }
            return projects;
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
                    () => _hsxClient.CreateApplicationCAS(clusterName, "Financial Management",
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
                    () => _hsxClient.DeleteApplication(clusterName, "Financial Management", appName));
        }


        [Command("Returns true if the connected user has CreateApplication rights on the HFM cluster")]
        public bool UserHasCreateApplicationRights(
                [Parameter("The name of the cluster on which to check the user rights")]
                string clusterName,
                IOutput output)
        {
            bool hasAccess = false;
            HFM.Try("Checking if user has CreateApplication rights",
                    () => _hsxClient.DoesUserHaveCreateApplicationRights(clusterName, out hasAccess));
            if(output != null) {
                output.SetHeader("Cluster", "Create Application Rights", 28);
                output.WriteSingleRecord(clusterName, hasAccess ? "Yes" : "No");
            }
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
                    () => _hsxClient.DoesUserHaveSystemAdminRights(clusterName, out hasAdmin));
            if(output != null) {
                output.SetHeader("Cluster", "System Admin Rights", 20);
                output.WriteSingleRecord(clusterName, hasAdmin ? "Yes" : "No");
            }
            return hasAdmin;
        }
    }

}
