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

        // Depth of recursion
        private int _depth = 0;

        /// <summary>
        /// Enumeration of available document types
        /// </summary>
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

        /// <summary>
        /// Enumeration of available document file types
        /// </summary>
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

        /// <summary>
        /// Enumeration of possible visibility choices
        /// </summary>
        public enum EPublicPrivate
        {
            Public = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_DONTSHOW,
            Private = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_SHOW,
            Both = tagENUMSHOWPRIVATEDOCS.ENUMSHOWPRIVATEDOCS_SHOWALL
        }


        public class DocumentInfo
        {
            public string Name;
            public string Path;
            public string Description;
            public EDocumentType DocumentType;
            public EDocumentFileType DocumentFileType;
            public DateTime Timestamp;
            public string DocumentOwner;
            public int SecurityClass;
            public bool IsPrivate;
            public EDocumentType FolderContentType;

            public bool IsFolder { get { return DocumentType == EDocumentType.Folder; } }
        }




        [Factory]
        public Documents(WebSession session)
        {
            _documents = new HFMwManageDocuments();
            _documents.SetWebSession(session.HFMwSession);
        }


        [Command("Returns a listing of documents that satisfy the search criteria")]
        public List<DocumentInfo> EnumDocuments(
                [Parameter("The document repository folder that contains the documents to return")]
                string path,
                [Parameter("An optional pattern that document names should match; may include wildcards ? and *",
                           DefaultValue = '*')]
                string namePattern,
                [Parameter("If true, recurses into each sub-folder encountered, and returns its content as well",
                           DefaultValue = false)]
                bool includeSubFolders,
                [Parameter("The document type(s) to return",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentTypes,
                [Parameter("The document file type(s) to return",
                           DefaultValue = EDocumentFileType.All)]
                EDocumentFileType documentFileTypes,
                [Parameter("A visibility setting used to determine whether public, private or both " +
                           "types of documents should be returned",
                 DefaultValue = EPublicPrivate.Public)]
                EPublicPrivate visibility,
                IOutput output)
        {
            object oNames = null, oDescs = null, oTimestamps = null, oSecurityClasses = null,
                   oIsPrivate = null, oFolderContentTypes = null, oDocOwners = null,
                   oFileTypes = null, oDocTypes = null;
            string[] names, descs, docOwners;
            int[] securityClasses;
            double[] timestamps;
            int[] isPrivate;
            EDocumentType[] folderContentTypes, docTypes;
            EDocumentFileType[] fileTypes;
            var nameRE = Utilities.ConvertWildcardPatternToRE(namePattern);
            var docs = new List<DocumentInfo>();

            HFM.Try(string.Format("Retrieving documents at {0}", path), () =>
                    oNames = _documents.EnumDocumentsEx(path, documentTypes, documentFileTypes,
                        false, 0, 0, (int)visibility,  // Note: timestamp filtering does not work
                        ref oDescs, ref oTimestamps, ref oSecurityClasses, ref oIsPrivate,
                        ref oFolderContentTypes, ref oDocOwners, ref oFileTypes, ref oDocTypes));

            names = HFM.Object2Array<string>(oNames);
            descs = HFM.Object2Array<string>(oDescs);
            timestamps = HFM.Object2Array<double>(oTimestamps);
            securityClasses = HFM.Object2Array<int>(oSecurityClasses);
            isPrivate = HFM.Object2Array<int>(oIsPrivate);
            folderContentTypes = HFM.Object2Array<EDocumentType>(oFolderContentTypes);
            docOwners = HFM.Object2Array<string>(oDocOwners);
            fileTypes = HFM.Object2Array<EDocumentFileType>(oFileTypes);
            docTypes = HFM.Object2Array<EDocumentType>(oDocTypes);

            if(!path.EndsWith(@"\")) {
                path += @"\";
            }
            for(var i = 0; i < names.Length; ++i) {
                if(nameRE.IsMatch(names[i])) {
                    docs.Add(new DocumentInfo() {
                        Name = names[i],
                        Path = path + names[i],
                        Description = descs[i],
                        DocumentType = docTypes[i],
                        DocumentFileType = fileTypes[i],
                        Timestamp = DateTime.FromOADate(timestamps[i]),
                        IsPrivate = isPrivate[i] != 0,
                        DocumentOwner = docOwners[i],
                        SecurityClass = securityClasses[i],
                        FolderContentType = folderContentTypes[i]
                    });
                }

                if(includeSubFolders && docTypes[i] == EDocumentType.Folder) {
                    // Recurse into sub-directory
                    _depth++;
                    docs.AddRange(EnumDocuments(path + names[i], namePattern, includeSubFolders,
                            documentTypes, documentFileTypes, visibility, output));
                    _depth--;
                }
            }

            if(output != null && _depth == 0) {
                output.SetFields("Name", "Document Type", "Timestamp", "Description");
                foreach(var doc in docs) {
                    output.WriteRecord(doc.Name, doc.DocumentType.ToString(), doc.Timestamp.ToString(), doc.Description);
                }
            }

            return docs;
        }


        [Command("Returns true if the specified document exists, or false if it does not")]
        public bool DoesDocumentExist(
                [Parameter("The path to the folder in which to check for the docuemnt")]
                string path,
                [Parameter("The name of the document")]
                string name,
                [Parameter("The document type to look for; use All to check all documents",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentType,
                [Parameter("The document file type to look for; use All to check all documents",
                           DefaultValue = EDocumentFileType.All)]
                EDocumentFileType documentFileType)
        {
            bool exists = false;

            HFM.Try("Checking if document exists",
                   () => exists = (bool)_documents.DoesDocumentExist(path, name, (int)documentType,
                                                                     (int)documentFileType));
            return exists;
        }


        [Command("Deletes a single document")]
        public bool DeleteDocument(
                [Parameter("The path to the folder containing the document to delete")]
                string path,
                [Parameter("The name of the document to delete")]
                string name)
        {
            bool deleted = false;
            string[] paths = new string[] { path };
            string[] names = new string[] { name };

            if(DoesDocumentExist(path, name, EDocumentType.All, EDocumentFileType.All)) {
                HFM.Try("Deleting document",
                        () => _documents.DeleteDocuments(paths, names, (int)EDocumentType.All,
                                                         (int)EDocumentFileType.All, false));
                deleted = !DoesDocumentExist(path, name, EDocumentType.All, EDocumentFileType.All);
                if(deleted) {
                    _log.InfoFormat("Successfully deleted document {0} from {1}", name, path);
                }
            }
            else {
                _log.WarnFormat("No document named {0} was found in folder {1}", name, path);
            }
            return deleted;
        }


        [Command("Deletes documents that match the search criteria")]
        public int DeleteDocuments(
                [Parameter("The path to the folder from which to delete documents")]
                string path,
                [Parameter("The name of the document(s) to delete; may include wildcards ? and *")]
                string namePattern,
                [Parameter("Set to true to delete matching documents in sub-folders as well",
                           DefaultValue = false)]
                bool includeSubFolders,
                [Parameter("The document type(s) to delete; use All to include all documents that " +
                           "match the name, path, and any other criteria",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentTypes,
                [Parameter("The document file type(s) to delete; use All to include all documents that " +
                           "match the name, path, and any other criteria",
                           DefaultValue = EDocumentFileType.All)]
                EDocumentFileType documentFileTypes)
        {
            int i = 0;
            string[] paths, names;
            List<DocumentInfo> docs = EnumDocuments(path, namePattern, includeSubFolders,
                    documentTypes, documentFileTypes, EPublicPrivate.Both, null);

            paths = new string[(docs.Count)];
            names = new string[(docs.Count)];
            foreach(var doc in docs) {
                paths[i] = doc.Path;
                names[i] = doc.Name;
                i++;
            }

            HFM.Try("Deleting documents",
                    () => _documents.DeleteDocuments(paths, names, (int)documentTypes,
                                                     (int)documentFileTypes, false));

            return i;
        }


        [Command("Creates a new folder")]
        public void CreateFolder(
                [Parameter("The path to the parent folder in which to create the new folder")]
                string path,
                [Parameter("The name of the new folder")]
                string folderName,
                [Parameter("A description for the new folder", DefaultValue = null)]
                string description,
                [Parameter("The name of the security class governing access to the folder",
                 DefaultValue = "[Default]")]
                string securityClass,
                [Parameter("Flag indicating whether the folder should be public (i.e. visible to other users) or private",
                 DefaultValue = false)]
                bool isPrivate,
                [Parameter("The document type(s) that are permitted within the folder",
                 DefaultValue = EDocumentType.All)]
                EDocumentType folderContentType,
                [Parameter("Flag indicating whether any existing folder with the same name (and its content) should be overwritten",
                 DefaultValue = false)]
                bool overwrite)
        {
            HFM.Try("Creating new folder",
                    () => _documents.CreateFolderEx(path, folderName, description, securityClass,
                                                    isPrivate, (int)folderContentType, overwrite));
            _log.InfoFormat(@"Folder created at {0}\{1}", path, folderName);
        }


        public void Load()
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
