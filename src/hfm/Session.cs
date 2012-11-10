using System;

using log4net;
using HSVSESSIONLib;
using HFMWSESSIONLib;

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

        // Reference to HFM HsvSession object
        private HsvSession _hsvSession;
        // Reference to a WebSession
        private HFMwSession _hfmwSession;
        // Reference to a Metadata object
        private Metadata _metadata;
        // Reference to a ProcessFlow object
        private ProcessFlow _processFlow;



        public Session(Connection conn, string cluster, string application)
        {
            _connection = conn;
            _cluster = cluster;
            _application = application;
        }


        internal HsvSession HsvSession
        {
            get {
                if(_hsvSession == null) {
                    object hsxServer = null, hsvSession = null;
                    HFM.Try(string.Format("Opening application {0} on {1}", _application, _cluster),
                            () => _connection.HsxClient.OpenApplication(_cluster,
                                    "Financial Management", _application,
                                    out hsxServer, out hsvSession));
                    _hsvSession = (HsvSession)hsvSession;
                }
                return _hsvSession;
            }
        }


        internal HFMwSession HFMwSession
        {
            get {
                if(_hfmwSession == null) {
                    object hfmwSession = null;
                    HFM.Try(string.Format("Opening web application {0} on {1}", _application, _cluster),
                            () => hfmwSession = _connection.HFMwManageApplications.OpenApplication(_cluster,
                                                    _application));
                    _hfmwSession = (HFMwSession)hfmwSession;
                }
                return _hfmwSession;
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
                        _processFlow = new PhasedSubmissionProcessFlow(this, Metadata);
                    }
                    else {
                        _processFlow = new ProcessUnitProcessFlow(this, Metadata);
                    }
                }
                return _processFlow;
            }
        }
    }

}
