using System;
using System.Collections.Generic;

using log4net;
using HSVSESSIONLib;
using HFMWDOCUMENTSLib;
using HFMCONSTANTSLib;

using Command;


namespace HFM
{

    /// <summary>
    /// Represents a class for working with HFM documents, such as web forms,
    /// data grids etc.
    /// <summary>
    /// TODO: Add following commands:
    /// - CreateFolder
    /// - DeleteDocuments
    /// - EnumDocuments
    /// - GetDocument
    /// - LoadDocuments
    /// - SaveDocument
    public class Documents
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HFMwManageDocuments object
        internal readonly HFMwManageDocuments _documents;

        public enum EDocumentType
        {
            All = tagDOCUMENTTYPES.WEBOM_DOCTYPE_ALL,
            Folder = tagDOCUMENTTYPES.WEBOM_DOCTYPE_FOLDER,
            Invalid = tagDOCUMENTTYPES.WEBOM_DOCTYPE_INVALID,
            Link = tagDOCUMENTTYPES.WEBOM_DOCTYPE_LINK,
            RelatedContent = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RELATEDCONTENT,
            DataExplorerReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTDATAEXPLORER,
            IntercompanyMatchByAccount = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHBYACCOUNT,
            IntercompanyMatchByTransactionID = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHBYTRANSID,
            IntercompanyMatchingTemplate = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHINGTEMPLATE,
            IntercompanyTransactionReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICTRANSACTION,
            IntercompayReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTINTERCOMPANY,
            JournalReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTJOURNAL,
            Task = tagDOCUMENTTYPES.WEBOM_DOCTYPE_TASK,
            DataForm = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WEBFORM,
            DataGrid = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WEBGRID,
            TaskList = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WORKSPACE
        }

        public class DocumentFilter //: ISettingsCollection
        {
            List<EDocumentType> _documentTypes = new List<EDocumentType>();
        }


        [Factory]
        public Documents(Session session)
        {
            // TODO: Determine if this needs to use the HFMwSession, HFMwManageApplications etc libraries
            _documents = (HFMwManageDocuments)session.HsvSession.CreateObject("Hyperion.HFMwManageDocuments");
            _documents.SetWebSession(session.HsvSession);
        }


        [Command("Returns a list of documents that satisfy the search criteria")]
        public void EnumDocuments(
                [Parameter("The document repository folder that contains the documents to return")]
                string path,
                [Parameter("The document type to return",
                 DefaultValue = tagDOCUMENTTYPES.WEBOM_DOCTYPE_ALL, EnumPrefix = "WEBOM_DOCTYPE_")]
                tagDOCUMENTTYPES documentTypes,
                tagDOCUMENTFILETYPES documentFileTypes,
                double startTime,
                double endTime,
                long includePrivateDocs)
        {
        }

    }


    /// <summary>
    /// Represents a document instance that is an HFM task list. The methods on
    /// this class correspond to those on the HFMwWorkspace object.
    /// </summary>
    public class TaskList
    {
    }
}
