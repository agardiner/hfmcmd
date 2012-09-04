using System;
using System.IO;
using System.Runtime.InteropServices;

using log4net;
using HSXCLIENTLib;
using HSVSESSIONLib;

using Command;


namespace HFM
{

    public class Client
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HsxClient COM object
        protected readonly HsxClient _client;


        // Constructor
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
        [Command]
        public void SetLogonInfo([DefaultValue(null)] string domain,
                string userName, [SensitiveValue] string password)
        {
            _log.Debug("Setting logon credentials via username and password");
            _client.SetLogonInfoSSO(domain, userName, null, password);
        }


        /// Sets the user credentials via an SSO token.
        [Command]
        public void SetLogonToken(string token)
        {
            _log.Debug("Setting logon credentials via SSO token");
            _client.SetLogonInfoSSO(null, null, token, null);
        }


        /// Opens the named application, and returns a Session object for
        /// interacting with it.
        [Command]
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
