using System;

using log4net;
#if !LATE_BIND
using HSVSESSIONLib;
using HFMWSESSIONLib;
using HSXSERVERLib;
#endif

using Command;


namespace HFM
{

    /// <summary>
    /// Represents a connection to a single HFM application. The main purpose of
    /// a Session is to obtain references to other functional modules for the
    /// current application.
    /// Instances of this class are created by a Factory method on the
    /// Connection class in the Client module.
    /// </summary>
    public class Session
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to Connection object
        private readonly Connection _connection;
        // Cluster name this session is with
        private readonly string _cluster;
        // Application name this session is with
        private readonly string _application;

#if LATE_BIND
        // Reference to HFM HsvSession object
        private dynamic _hsvSession;
        // Reference to a WebSession
        private dynamic _hfmwSession;
#else
        // Reference to HFM HsvSession object
        private HsvSession _hsvSession;
        // Reference to a WebSession
        private HFMwSession _hfmwSession;
#endif
        // Reference to a Server object
        private Server _server;
        // Reference to a Metadata object
        private Metadata _metadata;
        // Reference to a ProcessFlow object
        private ProcessFlow _processFlow;
        // Reference to a Security object
        private Security _security;
        // Reference to a Data object
        private Data _data;
        // Reference to a Calculate object
        private Calculate _calculate;
        // Reference to a SystemInfo object
        private SystemInfo _systemInfo;


        public Session(Connection conn, string cluster, string application)
        {
            _log.Trace("Constructing Session object");
            _connection = conn;
            _cluster = cluster;
            _application = application;
        }


#if LATE_BIND
        internal dynamic HsvSession
#else
        internal HsvSession HsvSession
#endif
        {
            get {
                if(_hsvSession == null) {
                    InitSession();
                }
                return _hsvSession;
            }
        }


#if LATE_BIND
        internal dynamic HFMwSession
#else
        internal HFMwSession HFMwSession
#endif
        {
            get {
                if(_hfmwSession == null) {
                    object hfmwSession = null;
                    HFM.Try(string.Format("Opening web application {0} on {1}", _application, _cluster),
                            () => hfmwSession = _connection.HFMwManageApplications.OpenApplication(_cluster,
                                                    _application));
#if LATE_BIND
                    _hfmwSession = hfmwSession;
#else
                    _hfmwSession = (HFMwSession)hfmwSession;
#endif
                }
                return _hfmwSession;
            }
        }


        public Server Server
        {
            get {
                if(_server == null) {
                    InitSession();
                }
                return _server;
            }
        }


        [Factory]
        public Metadata Metadata
        {
            get {
                if(_metadata == null) {
                    _metadata = new Metadata(this);
                }
                return _metadata;
            }
        }


        [Factory]
        public ProcessFlow ProcessFlow
        {
            get {
                if(_processFlow == null) {
                    if(Metadata.UsesPhasedSubmissions) {
                        _processFlow = new PhasedSubmissionProcessFlow(this);
                    }
                    else {
                        _processFlow = new ProcessUnitProcessFlow(this);
                    }
                }
                return _processFlow;
            }
        }


        [Factory]
        public Security Security
        {
            get {
                if(_security == null) {
                    _security = new Security(this);
                }
                return _security;
            }
        }


        [Factory]
        public Data Data
        {
            get {
                if(_data == null) {
                    _data = new Data(this);
                }
                return _data;
            }
        }


        [Factory]
        public Calculate Calculate
        {
            get {
                if(_calculate == null) {
                    _calculate = new Calculate(this);
                }
                return _calculate;
            }
        }


        [Factory]
        public SystemInfo SystemInfo
        {
            get {
                if(_systemInfo == null) {
                    _systemInfo = new SystemInfo(this);
                }
                return _systemInfo;
            }
        }


        private void InitSession()
        {
            object hsxServer = null, hsvSession = null;
            HFM.Try(string.Format("Opening application {0} on {1}", _application, _cluster),
                    () => _connection.Client.HsxClient.OpenApplication(_cluster,
                            "Financial Management", _application,
                            out hsxServer, out hsvSession));
#if LATE_BIND
            _hsvSession = hsvSession;
            _server = new Server(hsxServer);
#else
            _hsvSession = (HsvSession)hsvSession;
            _server = new Server((HsxServer)hsxServer);
#endif
        }
    }

}
