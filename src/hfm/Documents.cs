using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml;

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
    public class Documents
    {

        /// <summary>
        /// Enumeration of available document types
        /// </summary>
        public enum EDocumentType
        {
            All = tagDOCUMENTTYPES.WEBOM_DOCTYPE_ALL,
            Custom = tagDOCUMENTTYPES.WEBOM_DOCTYPE_CUSTOM,
            Folder = tagDOCUMENTTYPES.WEBOM_DOCTYPE_FOLDER,
            Invalid = tagDOCUMENTTYPES.WEBOM_DOCTYPE_INVALID,
            Link = tagDOCUMENTTYPES.WEBOM_DOCTYPE_LINK,
            RelatedContent = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RELATEDCONTENT,
            DataExplorerReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTDATAEXPLORER,
            IntercompanyMatchByAccount = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHBYACCOUNT,
            IntercompanyMatchByTransactionID = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHBYTRANSID,
            IntercompanyMatchingTemplate = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICMATCHINGTEMPLATE,
            IntercompanyTransactionReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTICTRANSACTION,
            IntercompanyReport = tagDOCUMENTTYPES.WEBOM_DOCTYPE_RPTINTERCOMPANY,
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
            Custom = tagDOCUMENTFILETYPES.WEBOM_DOCFILETYPE_CUSTOM,
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



        /// <summary>
        /// Holds metadata about a document.
        /// </summary>
        public class DocumentInfo
        {
            public string Name;
            public string Folder;
            public string Description;
            public EDocumentType DocumentType;
            public EDocumentFileType DocumentFileType;
            public DateTime Timestamp;
            public string DocumentOwner;
            public int SecurityClass;
            public bool IsPrivate;
            public EDocumentType FolderContentType;
            public readonly string Content;

            public bool IsFolder { get { return DocumentType == EDocumentType.Folder; } }
            public string DefaultFileName
            {
                get {
                    switch(DocumentType) {
                        case EDocumentType.Folder:
                        case EDocumentType.Custom:
                            return Name;
                        case EDocumentType.DataExplorerReport:
                            return Name + ".hde";
                        case EDocumentType.IntercompanyMatchByAccount:
                        case EDocumentType.IntercompanyMatchByTransactionID:
                        case EDocumentType.IntercompanyMatchingTemplate:
                        case EDocumentType.IntercompanyTransactionReport:
                        case EDocumentType.IntercompanyReport:
                        case EDocumentType.JournalReport:
                            return Name + ".rpt";
                        case EDocumentType.WebForm:
                            return Name + ".wdf";
                        case EDocumentType.Link:
                        case EDocumentType.TaskList:
                        case EDocumentType.DataGrid:
                            return Name + ".xml";
                        default:
                            throw new ArgumentException(string.Format(
                                        "Document has invalid document file type {0}",
                                        DocumentFileType));
                    }
                }
            }


            public bool IsVisible(EPublicPrivate visibility)
            {
                return visibility == EPublicPrivate.Both ||
                       (visibility == EPublicPrivate.Public && !IsPrivate) ||
                       (visibility == EPublicPrivate.Private && IsPrivate);
            }

            public bool IsDocumentType(EDocumentType docType)
            {
                return docType == EDocumentType.All || docType == DocumentType;
            }

            public DocumentInfo()
            { }


            public DocumentInfo(string filePath)
            {
                Name = Path.GetFileNameWithoutExtension(filePath);
                DocumentType = EDocumentType.Invalid;
                Content = File.ReadAllText(filePath);
                if(Content.Trim().StartsWith("<")) {
                    ParseXML(filePath);
                }
                else {
                    ParseRpt(filePath);
                }
            }


            /// Determines the document type for Rpt style file
            protected void ParseRpt(string filePath)
            {
                var re = new Regex(@"^(ReportType|ReportLabel|ReportDescription|ReportSecurityClass)=(.*)", RegexOptions.IgnoreCase);
                using(StreamReader r = new StreamReader(filePath)) {
                    while(r.Peek() >= 0) {
                        var line = r.ReadLine();
                        var match = re.Match(line);
                        if(match.Success) {
                            switch(match.Groups[1].Value.ToUpper()) {
                                case "REPORTDESCRIPTION":
                                    Description = match.Groups[2].Value;
                                    break;
                                case "REPORTLABEL":
                                    Name = match.Groups[2].Value;
                                    break;
                                case "REPORTSECURITYCLASS":
                                    // TODO: Fix this... SecurityClass = match.Groups[2].Value;
                                    break;
                                case "REPORTTYPE":
                                    switch(match.Groups[2].Value.ToUpper()) {
                                        case "INTERCOMPANY":
                                            DocumentType = EDocumentType.IntercompanyReport;
                                            DocumentFileType = EDocumentFileType.ReportDefRPT;
                                            break;
                                        case "JOURNAL":
                                            DocumentType = EDocumentType.JournalReport;
                                            DocumentFileType = EDocumentFileType.ReportDefRPT;
                                            break;
                                        case "WEBFORM":
                                            DocumentType = EDocumentType.WebForm;
                                            DocumentFileType = EDocumentFileType.WebFormDef;
                                            break;
                                        case "DATAEXPLORER":
                                            DocumentType = EDocumentType.DataExplorerReport;
                                            DocumentFileType = EDocumentFileType.XML;
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                }
                if(DocumentType == EDocumentType.Invalid) {
                    DocumentType = EDocumentType.Custom;
                }
                _log.TraceFormat("File {0} has document type {1}", filePath, DocumentType);
            }


            /// Determines the document type for Rpt style file
            protected void ParseXML(string filePath)
            {
                using(XmlTextReader xr = new XmlTextReader(filePath)) {
                    xr.WhitespaceHandling = WhitespaceHandling.None;
                    xr.Read(); // Read the XML declaration node, advance to next tag
                    xr.Read(); // Read the root tag
                    switch(xr.Name.ToLower()) {
                        case "linkdoc":
                            DocumentType = EDocumentType.Link;
                            break;
                        case "griddef":
                            DocumentType = EDocumentType.DataGrid;
                            break;
                        case "workspace":
                            DocumentType = EDocumentType.TaskList;
                            break;
                        default:
                            throw new DocumentException("Cannot determine document type for file {0}", filePath);
                    }
                    Name = xr.GetAttribute("doc_name");
                    Description = xr.GetAttribute("doc_description");
                    DocumentFileType = EDocumentFileType.XML;
                    // TODO: Fix this... SecurityClass = xr.GetAttribute("doc_secclass");
                }
            }
        }


        public class DocumentException : Exception
        {
            public DocumentException(string format, params object[] items)
                : base(string.Format(format, items))
            { }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Reference to HFM HFMwManageDocuments object
        internal readonly HFMwManageDocuments _documents;
        // Cache of folder -> List<DocumentInfo>
        internal Dictionary<string, List<DocumentInfo>> _documentCache;



        [Factory]
        public Documents(Session session)
        {
            _log.Trace("Constructing Documents object");
            _documents = new HFMwManageDocuments();
            _documents.SetWebSession(session.HFMwSession);
            _documentCache = new Dictionary<string, List<DocumentInfo>>(StringComparer.OrdinalIgnoreCase);
            LoadCache(@"\", true);
        }


        /// <summary>
        /// Convenience method for extendin a path with another folder.
        /// Handles the corner case of the root folder.
        /// </summary>
        public static string AddFolderToPath(string path, string folder)
        {
            if(path == null || path.Length == 0) {
                path = @"\";
            }

            if(folder != null && folder.Length > 0) {
                return path.Length > 1 ? path + @"\" + folder : @"\" + folder;
            }
            else {
                return path;
            }
        }


        /// <summary>
        /// Loads the document cache with info about all documents, keyed by
        /// their folder paths.
        /// </summary>
        protected void LoadCache(string path, bool recurse)
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
            var docs = new List<DocumentInfo>();

            if(path == null || path.Length == 0) {
                path = @"\";
            }

            HFM.Try(string.Format("Retrieving details of all documents at {0}", path),
                    () => oNames = _documents.EnumDocumentsEx(path, EDocumentType.All, EDocumentFileType.All,
                        false, 0, 0, (int)EPublicPrivate.Both,
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

            for(var i = 0; i < names.Length; ++i) {
                docs.Add(new DocumentInfo() {
                    Name = names[i],
                    Folder = path,
                    Description = descs[i],
                    DocumentType = docTypes[i],
                    DocumentFileType = fileTypes[i],
                    Timestamp = DateTime.FromOADate(timestamps[i]),
                    IsPrivate = isPrivate[i] != 0,
                    DocumentOwner = docOwners[i],
                    SecurityClass = securityClasses[i],
                    FolderContentType = folderContentTypes[i]
                });

                if(recurse && docTypes[i] == EDocumentType.Folder) {
                    // Recurse into sub-directory
                    LoadCache(AddFolderToPath(path, names[i]), recurse);
                }
            }
            _documentCache[path] = docs;
        }


        /// <summary>
        /// Returns the details of all sub-folders in the given folder.
        /// </summary>
        public IEnumerable<DocumentInfo> GetFolders(string path, EPublicPrivate visibility)
        {
            if(_documentCache.ContainsKey(path)) {
                return _documentCache[path].Where(doc => doc.DocumentType == EDocumentType.Folder &&
                        doc.IsVisible(visibility));
            }
            else {
                return null;
            }
        }


        /// <summary>
        /// Takes a path, and returns the reference to the folder object it represents
        /// </summary>
        public DocumentInfo GetParentFolder(string path)
        {
            DocumentInfo doc = null;
            var re = new Regex(@"(.*)\\([^\\]+)(?:\\)?$");
            var match = re.Match(path);
            if(match.Success) {
                var parent = match.Groups[1].Value;
                if(parent.Length == 0) {
                    parent = @"\";
                }
                var name = match.Groups[2].Value;
                _log.DebugFormat("Parent folder of {0} is {1}", path, parent);
                doc = FindDocument(parent, name, EDocumentType.Folder);
            }
            if(doc == null) {
                throw new DocumentException("No parent folder could be found for {0}", path);
            }
            return doc;
        }


        /// <summary>
        /// Returns a DocumentInfo object for the requested document if it
        /// exists, or null if the document is not found.
        /// </summary>
        public DocumentInfo FindDocument(string path, string name, EDocumentType docType)
        {
            _log.TraceFormat("Searching for {0} at {1}", name, path);
            DocumentInfo docInfo = null;
            if(_documentCache.ContainsKey(path)) {
                var nameRE = Utilities.ConvertWildcardPatternToRE(name);
                docInfo = _documentCache[path].FirstOrDefault(doc =>
                        nameRE.IsMatch(doc.Name) && doc.IsDocumentType(docType));
            }
            return docInfo;
        }


        [Command("Returns a listing of documents that satisfy the search criteria")]
        public List<DocumentInfo> EnumDocuments(
                [Parameter("The document repository folder that contains the documents to return " +
                           @"(note: the root folder is '\')",
                           DefaultValue = @"\")]
                string path,
                [Parameter("An optional pattern that document names should match; may include wildcards ? and *",
                           DefaultValue = "*")]
                string name,
                [Parameter("If true, recurses into each sub-folder encountered, and returns its content as well",
                           DefaultValue = false)]
                bool includeSubFolders,
                [Parameter("The document type(s) to return",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentType,
                [Parameter("A visibility setting used to determine whether public, private or both " +
                           "types of documents should be returned",
                 DefaultValue = EPublicPrivate.Public)]
                EPublicPrivate visibility,
                IOutput output)
        {
            var nameRE = Utilities.ConvertWildcardPatternToRE(name);
            List<DocumentInfo> docs = new List<DocumentInfo>();

            if(_documentCache.ContainsKey(path)) {
                _log.TraceFormat("Locating matching documents at {0}", path);
                docs.AddRange(_documentCache[path].Where(doc => nameRE.IsMatch(doc.Name) &&
                         doc.IsDocumentType(documentType) && doc.IsVisible(visibility)));

                if(includeSubFolders) {
                    // Recurse into sub-directory
                    foreach(var folder in GetFolders(path, visibility)) {
                        docs.AddRange(EnumDocuments(AddFolderToPath(path, folder.Name), name,
                                      includeSubFolders, documentType, visibility, null));
                    }
                }
            }
            else {
                _log.WarnFormat("The path '{0}' does not exist", path);
            }

            if(output != null) {
                // TODO: Add support for outputting any field of DocumentInfo
                output.SetHeader("Name", 30, "Document Type", "Timestamp", "Description");
                foreach(var doc in docs) {
                    output.WriteRecord(doc.Name, doc.DocumentType,
                            doc.Timestamp, doc.Description);
                }
                output.End();
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
                EDocumentType documentType)
        {
            bool exists = FindDocument(path, name, documentType) != null;
            _log.TraceFormat("Document {0} {1} exist", name, exists ? "does" : "does not");
            return exists;
        }


        [Command("Returns the contents of a document as a string")]
        public byte[] GetDocument(
                [Parameter("The path to the folder from which to retrieve the docuemnt")]
                string path,
                [Parameter("The name of the document to retrieve")]
                string name,
                // Multiple documents with the same name, but different document types can exist
                // within a folder
                [Parameter("The document type to look for; as names need not be unique within a folder, " +
                           "the document type can be used to disambiguate the actual document required. " +
                           "However, if the document you are after is unique, you can specify a document " +
                           "type of 'All' to retrieve the first document with the specified name.",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentType)
        {
            string docContent = null;
            object desc = null, secClass = null;

            // Find the document in the cache to determine its actual type and file type
            var doc = FindDocument(path, name, documentType);
            if(doc != null) {
                HFM.Try("Retrieving document",
                        () => docContent = (string)_documents.GetDocument(doc.Folder, doc.Name,
                                           (int)doc.DocumentType, (int)doc.DocumentFileType,
                                           ref desc, ref secClass));
            }
            else {
                throw new DocumentException("No document named {0} could be found at {1}", name, path);
            }
            return Utilities.GetBytes(docContent);
        }


        [Command("Saves a document to the requested folder and name")]
        public void SaveDocument(
                [Parameter("The path to the folder in which to save the document")]
                string path,
                [Parameter("The name to give the document")]
                string name,
                [Parameter("The description to give the document")]
                string desc,
                [Parameter("The document type to save the document as")]
                EDocumentType documentType,
                [Parameter("The document file type to save the document as")]
                EDocumentFileType documentFileType,
                [Parameter("The content for the new document")]
                string content,
                [Parameter("The security class to assign the document",
                           DefaultValue = "[Default]")]
                string securityClass,
                [Parameter("If true, the document is saved as a private document; otherwise, it is public",
                           DefaultValue = false)]
                bool isPrivate,
                [Parameter("True to overwrite any existing document with the same name in the folder",
                           DefaultValue = true)]
                bool overwrite)

        {
            HFM.Try("Saving document {0} to {1}", name, path,
                    () => _documents.SaveDocument2(path, name, desc, (int)documentType,
                                                    (int)documentFileType, securityClass,
                                                    content, isPrivate, (int)EDocumentType.All,
                                                    overwrite));
            // Update cache
            LoadCache(path, false);
        }


        [Command("Deletes documents that match the search criteria")]
        public int DeleteDocuments(
                [Parameter("The path to the folder from which to delete documents")]
                string path,
                [Parameter("The name of the document(s) to delete; may include wildcards ? and *")]
                string name,
                [Parameter("Set to true to delete matching documents in sub-folders as well",
                           DefaultValue = false)]
                bool includeSubFolders,
                [Parameter("The document type(s) to delete; use All to include all documents that " +
                           "match the name, path, and any other criteria",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentType,
                [Parameter("Filter documents to be deleted to public, private or both",
                           DefaultValue = EPublicPrivate.Both)]
                EPublicPrivate visibility,
                IOutput output)
        {
            int count = 0;
            List<DocumentInfo> docs = EnumDocuments(path, name, includeSubFolders,
                    documentType, visibility, null);
            docs.Reverse();     // So we delete folder content before folders

            var paths = new string[1];
            var names = new string[1];
            if(docs.Count > 1) {
                output.InitProgress("Deleting documents", docs.Count);
            }
            foreach(var doc in docs) {
                paths[0] = doc.Folder;
                names[0] = doc.Name;
                HFM.Try("Deleting document {0}", doc.Name,
                        () => _documents.DeleteDocuments(paths, names, (int)doc.DocumentType,
                                                         (int)doc.DocumentFileType, false));
                count++;
                if(output.IterationComplete()) {
                    break;
                }
                if(doc.DocumentType == EDocumentType.Folder) {
                    _documentCache.Remove(AddFolderToPath(doc.Folder, doc.Name));
                }
            }
            output.EndProgress();
            // Update cache
            LoadCache(path, includeSubFolders);
            _log.InfoFormat("Successfully deleted {0} documents", count);

            return count;
        }


        [Command("Deletes the specified folder, including all its content and sub-folders")]
        public void DeleteFolder(
                [Parameter("The path to the folder to delete")]
                string path,
                IOutput output)
        {
            DeleteDocuments(path, "*", true, EDocumentType.All, EPublicPrivate.Both, output);

            if(path.Length > 1) {
                // Add folder itself to list of items to be deleted
                var doc = GetParentFolder(path);

                var paths = new string[] { doc.Folder };
                var names = new string[] { doc.Name };
                HFM.Try("Deleting folder {0}", doc.Name,
                        () => _documents.DeleteDocuments(paths, names, (int)doc.DocumentType,
                                                         (int)doc.DocumentFileType, false));
                if(doc.DocumentType == EDocumentType.Folder) {
                    _documentCache.Remove(AddFolderToPath(doc.Folder, doc.Name));
                }
            }
            _log.InfoFormat("Successfully deleted folder {0}", path);
        }


        [Command("Creates a new document folder in an HFM application")]
        public void CreateFolder(
                [Parameter("The full document path to the folder to be created")]
                string path,
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
            var folders = path.Split('\\');
            var parent = @"\";
            for(var i = 1; i < folders.Length; ++i) {
                if(!DoesDocumentExist(parent, folders[i], EDocumentType.Folder) ||
                   (overwrite && i == folders.Length - 1)) {
                    HFM.Try("Creating new folder",
                            () => _documents.CreateFolderEx(parent, folders[i], description, securityClass,
                                                            isPrivate, (int)folderContentType, overwrite));
                    _log.InfoFormat(@"Folder created at {0}", AddFolderToPath(parent, folders[i]));
                    // Update cache
                    LoadCache(parent, true);
                }
                parent += @"\" + folders[i];
            }
        }


        [Command("Loads matching documents from the file system to HFM")]
        public void LoadDocuments(
                [Parameter("The source path containing the documents to be loaded")]
                string sourceDir,
                [Parameter("A file name pattern identifying the documents to be loaded; " +
                           "wildcards may be used. Note that the name pattern does not " +
                           "apply to folder names when IncludeSubDirs is true.",
                           DefaultValue = "*")]
                string name,
                [Parameter("The folder in the HFM document repository where the documents should be placed")]
                string targetFolder,
                [Parameter("A flag indicating whether sub-directories below the source directory " +
                           "should also be loaded. If true, sub-folders will be created below the " +
                           "target folder with the same names as the sub-directories.",
                           DefaultValue = false)]
                bool includeSubDirs,
                [Parameter("The document type(s) to upload", DefaultValue = EDocumentType.All)]
                EDocumentType documentType,
                [Parameter("The security class to be assigned to the uploaded documents",
                           DefaultValue = "[Default]")]
                string securityClass,
                [Parameter("If true, the documents are created as private documents; otherwise they are public",
                           DefaultValue = false)]
                bool isPrivate,
                [Parameter("True to overwrite existing documents, false to leave existing documents unchanged",
                           DefaultValue = false)]
                bool overwrite,
                IOutput output)
        {
            var files = Utilities.FindMatchingFiles(sourceDir, name, includeSubDirs);
            var loaded = 0;

            _log.InfoFormat("Loading documents from {0} to {1}", sourceDir, targetFolder);
            output.InitProgress("Loading documents", files.Count);

            foreach(var filePath in files) {
                var file = Path.GetFileName(filePath);
                var tgtFolder = Path.Combine(targetFolder, Utilities.PathDifference(sourceDir, filePath));
                CreateFolder(tgtFolder, null, securityClass, isPrivate, EDocumentType.All, false);
                if(overwrite || !DoesDocumentExist(tgtFolder, file, EDocumentType.All)) {
                    var doc = new DocumentInfo(filePath);
                    if(doc.IsDocumentType(documentType)) {
                        _log.FineFormat("Loading {0} to {1}", file, targetFolder);
                        SaveDocument(targetFolder, doc.Name, doc.Description,
                                     doc.DocumentType, doc.DocumentFileType,
                                     doc.Content, securityClass, isPrivate, overwrite);
                        loaded++;
                    }
                }
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
            _log.InfoFormat("Successfully loaded {0} documents to {1}", loaded, targetFolder);
        }


        [Command("Extracts documents from the HFM repository")]
        public void ExtractDocuments(
                [Parameter("The source folder from which document extraction is to take place")]
                string path,
                [Parameter("A document name pattern identifying the documents to extract",
                           DefaultValue = '*')]
                string name,
                [Parameter("The file system path where extracted documents are to be placed")]
                string targetDir,
                [Parameter("A flag indicating whether documents in sub-folders should be included",
                           DefaultValue = false)]
                bool includeSubFolders,
                [Parameter("The document type(s) to be extracted",
                           DefaultValue = EDocumentType.All)]
                EDocumentType documentType,
                [Parameter("The document file type(s) to be extracted",
                           DefaultValue = EDocumentFileType.All)]
                EDocumentFileType documentFileTypes,
                [Parameter("The type of documents to extract (public, private, or both)",
                           DefaultValue = EPublicPrivate.Both)]
                EPublicPrivate visibility,
                [Parameter("Flag indicating whether existing files should be overwritten",
                           DefaultValue = true)]
                bool overwrite,
                IOutput output)
        {
            string tgtPath, filePath;
            int extracted = 0;

            var docs = EnumDocuments(path, name, includeSubFolders, documentType,
                                     visibility, null);
            if(!Directory.Exists(targetDir)) {
                Directory.CreateDirectory(targetDir);
            }
            output.InitProgress("Extracting documents", docs.Count);
            foreach(var doc in docs) {
                tgtPath = Path.Combine(targetDir, Utilities.PathDifference(path, doc.Folder));
                filePath = Path.Combine(tgtPath, doc.DefaultFileName);
                if(doc.IsFolder) {
                    if(!Directory.Exists(filePath)) {
                        _log.FineFormat("Creating directory {0}", filePath);
                        Directory.CreateDirectory(filePath);
                    }
                }
                else if(overwrite || !File.Exists(filePath)) {
                    _log.FineFormat("Extracting document {0} to {1}", doc.Name, filePath);
                    var content = GetDocument(doc.Folder, doc.Name, doc.DocumentType);
                    using (FileStream fs = File.Open(filePath, FileMode.OpenOrCreate,
                                                     FileAccess.Write, FileShare.None)) {
                        fs.Write(content, 0, content.Length);
                    }
                    // Update last modified time to match document timestamp
                    File.SetLastWriteTime(filePath, doc.Timestamp);
                    extracted++;
                }
                if(output.IterationComplete()) {
                    break;
                }
            }
            output.EndProgress();
            _log.InfoFormat("Successfully extracted {0} documents to {1}", extracted, targetDir);
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
