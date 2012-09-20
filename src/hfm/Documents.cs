using System;
using System.Collections.Generic;

using log4net;
using HSVSESSIONLib;
using HFMWDOCUMENTSLib;
using HFMCONSTANTSLib;

using Command;
using HFMCmd;


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
            WebForm = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WEBFORM,
            DataGrid = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WEBGRID,
            TaskList = tagDOCUMENTTYPES.WEBOM_DOCTYPE_WORKSPACE
        }

        public enum EDocumentFileType
        {
            All = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_ALL,
            Folder = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_FOLDER,
            WebFormDef = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_FORMDEF,
            ReportDefRPT = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_RPTDEF,
            ReportDefXML = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_RPTXML,
            ReportDefHTML = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_RPTHTML,
            XML = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_XML,
            Task = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_TASK
        }

        public enum EPublicPrivate
        {
            Public = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_DONTSHOW,
            Private = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_SHOW,
            Both = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_SHOWALL
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
        // TODO: Handle conversion of date/times to doubles
        public void EnumDocuments(
                [Parameter("The document repository folder that contains the documents to return")]
                string path,
                [Parameter("A list of document type(s) to return",
                           DefaultValue = EDocumentType.All)]
                EDocumentType[] documentTypes,
                [Parameter("A list of document file type(s) to return",
                           DefaultValue = EDocumentFileType.All)]
                EDocumentFileType[] documentFileTypes,
                [Parameter("Start time of the timestamp filtering range (0 for no start filter)",
                           DefaultValue = 0)]
                double startTime,
                [Parameter("End time of the timestamp filtering range (0 for no end-time filter)",
                           DefaultValue = 0)]
                double endTime,
                [Parameter("A visibility setting used to determine whether public, private or both " +
                           "types of documents should be returned",
                 DefaultValue = false)]
                EPublicPrivate visibility,
                IOutput output)
        {
            bool filterTimestamp = startTime > 0 || endTime > 0;
            object oNames = null, oDescs = null, oTimestamps = null, oSecurityClasses = null,
                   oIsPrivate = null, oFolderContentTypes = null, oDocOwners = null,
                   oFileTypes = null, oDocTypes = null;
            string[] names, descs, securityClasses, docOwners;
            double[] timestamps;
            bool[] isPrivate;
            EDocumentType[] folderContentTypes, docTypes;
            EDocumentFileType[] fileTypes;

            // TODO: Handle recursive paths
            HFM.Try("Retrieving documents", () =>
                    oNames = _documents.EnumDocumentsEx(path, documentTypes, documentFileTypes,
                        filterTimestamp, startTime, endTime, (int)visibility,
                        ref oDescs, ref oTimestamps, ref oSecurityClasses, ref oIsPrivate,
                        ref oFolderContentTypes, ref oDocOwners, ref oFileTypes, ref oDocTypes));

            names = oNames as string[];
            descs = oDescs as string[];
            timestamps = oTimestamps as double[];
            securityClasses = oSecurityClasses as string[];
            isPrivate = oIsPrivate as bool[];
            folderContentTypes = oFolderContentTypes as EDocumentType[];
            docOwners = oDocOwners as string[];
            fileTypes = oFileTypes as EDocumentFileType[];
            docTypes = oDocTypes as EDocumentType[];

            output.SetFields("Name", "Description", "Document Type");
            for(var i = 0; i < (names as string[]).Length; ++i) {
                output.WriteRecord(names[i], descs[i], docTypes[i].ToString());
            }
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
