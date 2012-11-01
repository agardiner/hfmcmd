using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using log4net;

using HSVRESOURCEMANAGERLib;
using HFMCONSTANTSLib;

using Command;
using HFMCmd;


namespace HFM
{

    public class ResourceManager
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HsvResourceManager used to obtain error message details.
        private static readonly HsvResourceManager _resourceManager;

        // Flag indicating whether we have already logged the absence of ResourceManager
        private static bool _loggedErrorMessagesUnavailable = false;

        /// The current version of HFM that is installed on this machine
        public static readonly Version Version;


        // Static initializer - initializes HsvResourceManager
        static ResourceManager()
        {
            try {
                _resourceManager = new HsvResourceManager();
                _resourceManager.Initialize((short)tagHFM_TIERS.HFM_TIER1);

                var ver = (string)_resourceManager.GetCurrentVersionInUserDisplayFormat();
                var re = new Regex(@"^(\d+\.\d+\.\d+(?:\.\d+)?)");
                var match = re.Match(ver);
                if(match.Success) {
                    Version = new Version(match.Groups[1].Value);
                }
                else {
                    throw new Exception(string.Format("Could not determine HFM version from " +
                               "returned version string '{0}'", ver));
                }
            }
            catch (COMException ex) {
                unchecked {
                    if(ex.ErrorCode == (int)0x80040154) {
                        _log.Error("Unable to instantiate an HsvResourceManager COM object; " +
                                   "is HFM installed on this machine?");
                    }
                }
                throw ex;
            }
        }


        [Factory]
        public ResourceManager()
        {
        }


        [Command("Displays the version of HFM that is installed")]
        public string GetHFMVersion(IOutput output)
        {
            var ver = (string)_resourceManager.GetCurrentVersionInUserDisplayFormat();
            if(output != null) {
                output.WriteSingleValue(ver, "Version");
            }
            return ver;
        }


        [Command("Displays the version and build number of HFM that is installed")]
        public string GetHFMBuild(IOutput output)
        {
            string ver = (string)_resourceManager.GetCurrentVersion();
            if(output != null) {
                output.WriteSingleValue(ver, "Version");
            }
            return ver;
        }


        public static string GetErrorMessage(int errorCode, string message)
        {
            return GetErrorMessage(errorCode, message, tagHFM_LANGUAGES.HFM_LANGUAGE_ENGLISH);
        }


        public static string GetErrorMessage(int errorCode, string message, tagHFM_LANGUAGES lang)
        {
            if(_resourceManager != null) {
                object formattedError, techError;
                _resourceManager.GetFormattedError((int)lang, errorCode, message,
                        "Unknown error", out formattedError, out techError);
                if(_log.IsDebugEnabled && techError != null) {
                    _log.Debug(techError);
                }
                return formattedError as string;
            }
            else {
                if(!_loggedErrorMessagesUnavailable) {
                    _log.Fatal("Unable to initialise HFM Resource Manager; error message strings will not be available");
                    _loggedErrorMessagesUnavailable = true;
                }
                return message;
            }
        }
    }

}
