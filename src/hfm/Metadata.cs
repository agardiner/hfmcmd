using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALib;
using HFMCONSTANTSLib;
using HFMSLICECOMLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Defines the possible sizes of a Custom dimension. The sizes determine
    /// the maximum number of members that may appear in the dimension, and are
    /// used when specifying the custom dimensions in an app (post 11.1.2.2).
    /// </summary>
    public enum ECustomDimSize : short
    {
        Small = 1,
        Medium = 2,
        Large = 4
    }


    /// <summary>
    /// Defines the names of the 8 fixed and 2 or more Custom dimensions
    /// in an HFM application.
    /// </summary>
    public enum EDimension
    {
        Scenario = tagHFMDIMENSIONS2.DIMID_SCENARIO,
        Year = tagHFMDIMENSIONS2.DIMID_YEAR,
        Period = tagHFMDIMENSIONS2.DIMID_PERIOD,
        View = tagHFMDIMENSIONS2.DIMID_VIEW,
        Entity = tagHFMDIMENSIONS2.DIMID_ENTITY,
        Value = tagHFMDIMENSIONS2.DIMID_VALUE,
        Account = tagHFMDIMENSIONS2.DIMID_ACCOUNT,
        ICP = tagHFMDIMENSIONS2.DIMID_ICP,
        CustomBase = tagHFMDIMENSIONS2.DIMID_CUSTOMBASE,
        Custom1 = tagHFMDIMENSIONS.DIMENSIONCUSTOM1,
        Custom2 = tagHFMDIMENSIONS.DIMENSIONCUSTOM2,
        Custom3 = tagHFMDIMENSIONS.DIMENSIONCUSTOM3,
        Custom4 = tagHFMDIMENSIONS.DIMENSIONCUSTOM4
    }



    /// <summary>
    /// Wraps the HsvMetadata module, exposing its functionality for querying
    /// metadata, obtaining member ids, etc.
    /// </summary>
    public class Metadata
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        // Names of the fixed dimensions
        public static string[] FixedDimNames = new string[] {
            "Scenario", "Year", "Period", "View", "Entity", "Value",
            "Account", "ICP"
        };

        // Reference to HFM HsvMetadata object
        protected readonly HsvMetadata _hsvMetadata;
        // True if the app supports phased submission groups
        protected bool? _usesPhasedSubmissions;
        // Cache of custom dimension ids
        protected int[] _customDimIds;
        // Cache of custom dimension names
        protected string[] _customDimNames;
        // Cache of custom dimension aliases
        protected string[] _customDimAliases;
        // Map of custom dimension names and aliases to ids
        protected Dictionary<string, int> _customDimMap;
        // Cache of dimensions
        protected Dimension[] _dimensions;


        /// Returns the HFM HsvMetadata object
        internal HsvMetadata HsvMetadata { get { return _hsvMetadata; } }
        /// Returns the Scenario dimension
        public Dimension Scenario { get { return this[EDimension.Scenario]; } }
        /// Returns the Year dimension
        public Dimension Year { get { return this[EDimension.Year]; } }
        /// Returns the Period dimension
        public Dimension Period { get { return this[EDimension.Period]; } }
        /// Returns the View dimension
        public Dimension View { get { return this[EDimension.View]; } }
        /// Returns the Entity dimension
        public Dimension Entity { get { return this[EDimension.Entity]; } }
        /// Returns the Value dimension
        public Dimension Value { get { return this[EDimension.Value]; } }
        /// Returns the Account dimension
        public Dimension Account { get { return this[EDimension.Account]; } }
        /// Returns the ICP dimension
        public Dimension ICP { get { return this[EDimension.ICP]; } }
        /// Returns the names of all dimensions in the current app
        public string[] DimensionNames
        {
            get {
                string[] names = new string[NumberOfDims];
                Array.Copy(FixedDimNames, names, 8);
                Array.Copy(CustomDimNames, 0, names, 8, NumberOfCustomDims);
                return names;
            }
        }
        internal int[] DimensionIds
        {
            get {
                int[] ids = new int[NumberOfDims];
                ids[0] = (int)EDimension.Scenario;
                ids[1] = (int)EDimension.Year;
                ids[2] = (int)EDimension.Period;
                ids[3] = (int)EDimension.View;
                ids[4] = (int)EDimension.Entity;
                ids[5] = (int)EDimension.Value;
                ids[6] = (int)EDimension.Account;
                ids[7] = (int)EDimension.ICP;
                Array.Copy(CustomDimIds, 0, ids, 8, NumberOfCustomDims);
                return ids;
            }
        }
        /// Returns a count of the total number of dimensions in the current app
        public int NumberOfDims { get { return FixedDimNames.Length + NumberOfCustomDims; } }
        /// Returns a count of the number of custom dimensions in the current app
        public int NumberOfCustomDims
        {
            get {
                if(_customDimIds == null) {
                    GetCustomDims();
                }
                return _customDimIds.Length;
            }
        }
        /// Returns the Custom dimension ids
        internal int[] CustomDimIds
        {
            get {
                if(_customDimIds == null) {
                    GetCustomDims();
                }
                return _customDimIds;
            }
        }
        /// Returns the Custom dimension names
        public string[] CustomDimNames
        {
            get {
                if(_customDimNames == null) {
                    GetCustomDims();
                }
                return _customDimNames;
            }
        }
        /// Returns the Custom dimension aliases
        internal string[] CustomDimAliases
        {
            get {
                if(_customDimAliases == null) {
                    GetCustomDims();
                }
                return _customDimAliases;
            }
        }
        /// Returns a Dimension object for the specified dimension
        public Dimension this[string dimName]
        {
            get {
                int dimId = GetDimensionId(ref dimName);
                return this[dimId];
            }
        }
        /// Returns a Dimension object for the specified dimension
        internal Dimension this[EDimension dim]
        {
            get {
                return this[(int)dim];
            }
        }
        /// Returns a Dimension object for the specified dimension id
        internal Dimension this[int dimId]
        {
            get {
                CheckDimId(dimId);
                if(_dimensions[dimId] == null) {
                    string dimName = DimensionNames[dimId];
                    IHsvTreeInfo dim = null;
                    HFM.Try("Retrieving dimension {0}", dimName,
                            () => dim = (IHsvTreeInfo)_hsvMetadata.GetDimension(dimId));
                    _dimensions[dimId] = new Dimension(dimName, dimId, this, dim);
                }
                return _dimensions[dimId];
            }
        }
        /// Returns true if the application is configured to use phased submissions
        public bool UsesPhasedSubmissions
        {
            get {
                if(_usesPhasedSubmissions == null) {
                    bool usesPhasedSubmissions = false;
                    HFM.Try("Retrieving phased submission flag",
                            () => _hsvMetadata.GetUseSubmissionPhaseFlag(out usesPhasedSubmissions));
                    _usesPhasedSubmissions = usesPhasedSubmissions;
                }
                return (bool)_usesPhasedSubmissions;
            }
        }


        /// Constructor
        internal Metadata(Session session)
        {
            _log.Trace("Constructing Metadata object");
            _hsvMetadata = (HsvMetadata)session.HsvSession.Metadata;
            _dimensions = new Dimension[NumberOfDims];
        }


        /// Given a dimension name, returns the id for that dimension.
        internal int GetDimensionId(ref string dimName)
        {
            int dimId;

            switch(dimName.ToUpper()) {
                case "SCENARIO":
                case "S":
                    dimName = "Scenario";
                    dimId = (int)EDimension.Scenario;
                    break;
                case "YEAR":
                case "Y":
                    dimId = (int)EDimension.Year;
                    dimName = "Year";
                    break;
                case "PERIOD":
                case "P":
                    dimId = (int)EDimension.Period;
                    dimName = "Period";
                    break;
                case "VIEW":
                case "W":
                    dimId = (int)EDimension.View;
                    dimName = "View";
                    break;
                case "ENTITY":
                case "E":
                    dimId = (int)EDimension.Entity;
                    dimName = "Entity";
                    break;
                case "VALUE":
                case "V":
                    dimId = (int)EDimension.Value;
                    dimName = "Value";
                    break;
                case "ACCOUNT":
                case "A":
                    dimId = (int)EDimension.Account;
                    dimName = "Account";
                    break;
                case "ICP":
                case "I":
                    dimId = (int)EDimension.ICP;
                    dimName = "ICP";
                    break;
                default:
                    if(_customDimNames == null) {
                        GetCustomDims();
                    }
                    if(HFM.HasVariableCustoms) {
                        var re = new Regex(@"^Custom(\d+)$", RegexOptions.IgnoreCase);
                        var match = re.Match(dimName);
                        int custom = -1;
                        if(match.Success) {
                            custom = int.Parse(match.Groups[1].Value) - 1;
                            if(custom < _customDimIds.Length) {
                                dimId = (int)EDimension.CustomBase + custom;
                                dimName = _customDimNames[custom];
                            }
                            else {
                                throw new ArgumentException(string.Format("Unknown dimension name '{0}'", dimName));
                            }
                        }
                        else {
                            if(_customDimMap.ContainsKey(dimName)) {
                                custom = _customDimMap[dimName];
                                dimId = _customDimIds[custom];
                                dimName = _customDimNames[custom];
                            }
                            else {
                                throw new ArgumentException(string.Format("Unknown dimension name '{0}'", dimName));
                            }
                        }
                    }
                    else {
                        throw new ArgumentException(string.Format("Unknown dimension name '{0}'", dimName));
                    }
                    break;
            }
            return dimId;
        }


        /// Populates the internal variables holding the names and dimension ids
        /// for the custom dimensions in the current app.
        private void GetCustomDims()
        {
            _customDimMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if(HFM.HasVariableCustoms) {
                object oDimIds = null, oDimNames = null, oDimAliases = null, oSizes = null;
                HFM.Try("Retrieving custom dimension details",
                        () => _hsvMetadata.EnumCustomDimsForAppEx(out oDimIds, out oDimNames,
                                                                  out oDimAliases, out oSizes));
                _customDimIds = (int[])oDimIds;
                _customDimNames = HFM.Object2Array<string>(oDimNames);
                _customDimAliases = HFM.Object2Array<string>(oDimAliases);
                for(var i = 0; i < _customDimIds.Length; ++i) {
                    _customDimMap[_customDimNames[i]] = i;
                    _customDimMap[_customDimAliases[i]] = i;
                }
            }
            else {
                _customDimIds = new int[] {
                    (int)EDimension.Custom1, (int)EDimension.Custom2,
                    (int)EDimension.Custom3, (int)EDimension.Custom4
                };
                _customDimNames = new string[] {
                    "Custom1", "Custom2", "Custom3", "Custom4"
                };
                _customDimAliases = new string[] {
                    "C1", "C2", "C3", "C4"
                };
            }
        }


        /// Checks the specified dimension id is valid for this application,
        /// throwing an IndexOutOfRangeException if it is not.
        internal void CheckDimId(int dimId)
        {
            if(dimId < 0 || dimId >= NumberOfDims) {
                throw new IndexOutOfRangeException(string.Format("Invalid dimension number {0}; " +
                            "must be a value between 0 and {1}", dimId, NumberOfDims - 1));
            }
        }


        /// Checks the specified dimension id is a valid Custom dimension for
        /// this application, throwing an IndexOutOfRangeException if it is not.
        internal int CheckCustomDimId(int dimId)
        {
            if(dimId < (int)EDimension.CustomBase || dimId >= NumberOfDims) {
                throw new IndexOutOfRangeException(string.Format("Invalid custom dimension id {0}; " +
                            "must be a value between {1} and {2}", dimId,
                            (int)EDimension.CustomBase, NumberOfDims - 1));
            }
            return dimId - (int)EDimension.CustomBase + 1;
        }


        /// Converts a Custom dimension number to a dimension id
        internal int GetDimIdForCustom(int customNum)
        {
            if(customNum < 1 || customNum > NumberOfCustomDims) {
                throw new IndexOutOfRangeException(string.Format("Invalid Custom dimension number {0}; " +
                            "must be a value between 1 and {1}", customNum, NumberOfCustomDims));
            }
            return (int)EDimension.CustomBase + customNum - 1;
        }


        /// Returns the Account member that is the prinary validation account
        internal Account GetValidationAccount()
        {
            int id = Member.NOT_USED;
            HFM.Try("Retrieving validation account",
                    () => _hsvMetadata.GetValidationAccount(out id));
            return new Account(this[EDimension.Account], id);
        }


        /// Returns the Account member that is the prinary validation account
        internal Account GetPhaseValidationAccount(int phaseId)
        {
            int id = Member.NOT_USED;
            HFM.Try("Retrieving phased validation account",
                    () => _hsvMetadata.GetByIndexValidationAccount(phaseId, out id));
            return new Account(this[EDimension.Account], id);
        }


        [Command("Lists the cell text labels for an application",
                 Since = "11.1.2.2")]
        public void EnumCellTextLabels(IOutput output)
        {
            object oIds = null, oLabels = null;
            int[] ids;
            string[] labels;

            HFM.Try("Retrieving cell text labels",
                    () => _hsvMetadata.EnumCellTextLabels(out oIds, out oLabels));
            ids = (int[])oIds;
            labels = HFM.Object2Array<string>(oLabels);
            output.WriteEnumerable(labels, "Label");
            // TODO: Work out how to return labels and ids; array of Members?
        }


        [Command("Lists the custom dimension names, aliases, and sizes for an application",
                 Since = "11.1.2.2")]
        public void EnumCustomDims(IOutput output)
        {
            object oDimIds = null, oDimNames = null, oDimAliases = null, oSizes = null;
            int[] dimIds;
            string[] dimNames, dimAliases;
            ECustomDimSize[] sizes;

            HFM.Try("Retrieving custom dimension details",
                    () => _hsvMetadata.EnumCustomDimsForAppEx(out oDimIds, out oDimNames,
                                                              out oDimAliases, out oSizes));
            dimIds = (int[])oDimIds;
            dimNames = HFM.Object2Array<string>(oDimNames);
            dimAliases = HFM.Object2Array<string>(oDimAliases);
            sizes = (ECustomDimSize[])oSizes;

            output.SetHeader("Dimension Name", "Dimension Alias", "Size", 8);
            for(int i = 0; i < dimIds.Length; ++i) {
                output.WriteRecord(dimNames[i], dimAliases[i], sizes[i]);
            }
            output.End();
            // TODO: Work out how to return this
        }


        [Command("Returns the names of the member lists for a dimension")]
        public string[] EnumMemberLists(
            [Parameter("The dimension whose member lists are to be returned")]
            string dimension,
            IOutput output)
        {
            return this[dimension].EnumMemberLists(output);
        }


        [Command("Returns the members in a dimension. If no member list(s) are " +
                 "specified, all dimension members are returned.")]
        public void EnumMembers(
            [Parameter("The dimension whose members are to be returned")]
            string dimension,
            [Parameter("The member list(s) containing the members are to be returned; " +
                       "should be enclosed in { and }, e.g. {[Base]}, {Sports Equipment}, etc. " +
                       "If no member lists are specified, all members in the dimension are returned.",
                       Alias = "MemberList", DefaultValue = "{[Hierarchy]}")]
            string[] memberLists,
            IOutput output)
        {
            var members = this[dimension].GetMembers(memberLists);
            output.WriteEnumerable(members, "Member");
        }


        [Command("Returns details about phased submissions")]
        public void GetPhasedSubmissionDetails(IOutput output)
        {
            output.WriteSingleValue(string.Format("Phased submission groups are {0}",
                        UsesPhasedSubmissions ? "enabled" : "disabled"));
            if(UsesPhasedSubmissions) {
                output.SetHeader("Dimension", "Phased Submission Enabled");
                output.WriteRecord("Account", IsPhasedSubmissionEnabledForDimension((int)EDimension.Account));
                output.WriteRecord("ICP", IsPhasedSubmissionEnabledForDimension((int)EDimension.ICP));
                foreach(var id in CustomDimIds) {
                    output.WriteRecord(CustomDimAliases[id], IsPhasedSubmissionEnabledForDimension(id));
                }
                output.End(true);
            }
        }


        /// <summary>
        /// Returns true if the dimension supports phased submissions
        /// </summary>
        public bool IsPhasedSubmissionEnabledForDimension(int dimId)
        {
            bool flag = false;
            switch(dimId) {
                case (int)EDimension.Account:
                    HFM.Try("Retrieving Account phased submission flag",
                            () => _hsvMetadata.GetSupportSubmissionPhaseForAccountFlag(out flag));
                    break;
                case (int)EDimension.ICP:
                    HFM.Try("Retrieving ICP phased submission flag",
                            () => _hsvMetadata.GetSupportSubmissionPhaseForICPFlag(out flag));
                    break;
                default:
                    if(dimId >= (int)EDimension.CustomBase && dimId < NumberOfCustomDims) {
                        HFM.Try("Retrieving Custom phased submission flag", () => {
                            if(HFM.HasVariableCustoms) {
                                _hsvMetadata.GetSupportSubmissionPhaseForCustomXFlag(dimId, out flag);
                            }
                            else {
                                switch(dimId) {
                                    case (int)EDimension.Custom1:
                                        _hsvMetadata.GetSupportSubmissionPhaseForCustom1Flag(out flag);
                                        break;
                                    case (int)EDimension.Custom2:
                                        _hsvMetadata.GetSupportSubmissionPhaseForCustom2Flag(out flag);
                                        break;
                                    case (int)EDimension.Custom3:
                                        _hsvMetadata.GetSupportSubmissionPhaseForCustom3Flag(out flag);
                                        break;
                                    case (int)EDimension.Custom4:
                                        _hsvMetadata.GetSupportSubmissionPhaseForCustom4Flag(out flag);
                                        break;
                                }
                            }
                        });
                    }
                    break;
            }
            return flag;
        }


        /// <summary>
        /// Takes a slice specification, and returns a Slice object.
        /// </summary>
        public Slice Slice(string pov)
        {
            return new Slice(this, pov);
        }

    }



    /// <summary>
    /// Represents an HFM dimension, and contains functionality for obtaining
    /// members, enumerating lists, etc.
    /// <summary>
    public class Dimension
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to the Metadata object
        protected Metadata _metadata;
        // Reference to HFM HsvTreeInfo object
        protected IHsvTreeInfo _hsvTreeInfo;
        // Name of the dimension
        protected string _name;
        // Id of the dimension
        protected int _id;
        // Id for the [Hierarchy] member list (to return all members in the dimension)
        public const int MEMBER_LIST_ALL_HIERARCHY = 0;
        // Member id for the root of a hierarchy
        public const int TREE_ROOT = -1;


        // Properties

        /// Internal access to the Metadata object
        internal Metadata Metadata { get { return _metadata; } }
        /// Internal access to the underlying HsvTreeInfo COM object
        internal IHsvTreeInfo HsvTreeInfo { get { return _hsvTreeInfo; } }
        /// The name of the dimension
        public string Name { get { return _name; } }
        /// The id of the dimension
        public int Id { get { return _id; } }
        /// True if this is the Entity dimension
        public bool IsEntity { get { return _id == (int)EDimension.Entity; } }
        /// True if this is the Accounts dimension
        public bool IsAccount { get { return _id == (int)EDimension.Account; } }


        /// Constructor
        internal Dimension(string dimension, int id, Metadata metadata, IHsvTreeInfo treeInfo)
        {
            _name = dimension;
            _id = id;
            _metadata = metadata;
            _hsvTreeInfo = treeInfo;
        }


        /// <summary>
        /// Returns the member id for a member with the specified label.
        /// </summary>
        public int GetId(string member)
        {
            int id = -1;

            HFM.Try("Retrieving member id for {0} {1}", Name, member,
                    () => id = HsvTreeInfo.GetItemID(member));

            if (id >= 0) {
                return id;
            }
            else {
                throw new ArgumentException("No member named '" + member + "' exists in dimension " + Name);
            }
        }


        /// <summary>
        /// Returns the member label for a member with the specified id.
        /// </summary>
        public string GetLabel(int id)
        {
            string label = null;

            HFM.Try("Retrieving member label for {0} id {1}", Name, id,
                    () => id = HsvTreeInfo.GetLabel(id, out label));

            return label;
        }


        /// <summary>
        /// Returns the member list id for the specified member list name.
        /// </summary>
        public int GetMemberListId(string listName)
        {
            int id = -1;

            HFM.Try("Retrieving id for {0} member list {0}", Name, listName,
                    () => HsvTreeInfo.GetMemberListID(listName, out id));

            if (id >= 0) {
                return id;
            }
            else {
                throw new ArgumentException("No member list named '" + listName +
                        "' exists in dimension " + Name);
            }
        }


        /// <summary>
        /// Returns an array containing the labels of all members in the
        /// dimension.
        /// </summary>
        public string[] EnumAllMembers(IOutput output)
        {
            object oLabels = null;
            string[] labels;
            HFM.Try("Retrieving member labels",
                    () => HsvTreeInfo.EnumAllMemberLabels(out oLabels));
            labels = HFM.Object2Array<string>(oLabels);

            output.WriteEnumerable(labels, "Label");

            return labels;
        }


        /// <summary>
        /// Returns an array containing the names of all member lists in the
        /// dimension.
        /// </summary>
        public string[] EnumMemberLists(IOutput output)
        {
            object oLists = null;
            string[] lists;
            HFM.Try("Retrieving member lists",
                    () => HsvTreeInfo.EnumMemberLists(out oLists));
            lists = HFM.Object2Array<string>(oLists);

            output.WriteEnumerable(lists, "Member List");

            return lists;
        }


        /// <summary>
        /// Returns a MemberList corresponding to a single member spec.
        /// The memberSpec can correspond to a member name, member list name,
        /// or range of members.
        /// </summary>
        public MemberList GetMembers(string memberSpec)
        {
            return new MemberList(this, memberSpec);
        }


        /// <summary>
        /// Returns a MemberList corresponding to the member specs.
        /// The memberSpec can correspond to a member name, member list name,
        /// or range of members.
        /// </summary>
        public MemberList GetMembers(IEnumerable<string> memberSpecs)
        {
            return new MemberList(this, memberSpecs);
        }


        /// <summary>
        /// Returns a Member object for the member with the specified name
        /// </summary>
        public Member GetMember(string name)
        {
            switch(Id) {
                case (int)EDimension.Entity:
                    return new Entity(this, name);
                case (int)EDimension.Account:
                    return new Account(this, name);
                default:
                    return new Member(this, name);
            }
        }


        /// <summary>
        /// Returns a Member object for the member with the specified id
        /// </summary>
        public Member GetMember(int id)
        {
            return GetMember(id, Member.NOT_USED);
        }


        /// <summary>
        /// Returns a Member object for the member with the specified id
        /// </summary>
        public Member GetMember(int id, int parentId)
        {
            switch(Id) {
                case (int)EDimension.Entity:
                    return new Entity(this, id, parentId);
                case (int)EDimension.Account:
                    return new Account(this, id);
                default:
                    return new Member(this, id);
            }
        }


        public override string ToString()
        {
            return _name;
        }
    }



    /// <summary>
    /// Represents a member of a dimension (other than Entity).
    /// <summary>
    public class Member : IEquatable<Member>
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Regular expression class for matching allowed member names
        internal const string NAME_RE_CLASS = @"[^*@,{}""\-#.+;/ !][^*@,{}""\-#.+;/]*";
        /// Id indicating a parent id that has not been specified
        public const int NOT_USED = -1;
        /// Id value used when a member id has not yet been retrieved
        public const int ID_NEEDS_RETRIEVAL = -9;
        /// Reference to the Dimension to which this Member belongs
        protected Dimension _dimension;
        /// Name of this member
        protected string _name;
        /// Id of this member
        protected int _id = ID_NEEDS_RETRIEVAL;
        /// Parent name of this member
        protected string _parentName = null;
        /// Parent id of this member
        protected int _parentId = ID_NEEDS_RETRIEVAL;


        /// The internal id for the member
        public int Id
        {
            get {
                if(_id == ID_NEEDS_RETRIEVAL) {
                    _id = _dimension.GetId(_name);
                }
                return _id;
            }
        }
        /// The name of the member
        public string Name
        {
            get {
                if(_name == null) {
                    _name = _dimension.GetLabel(_id);
                }
                return _name;
            }
        }
        /// Returns the parent id for this member
        public int ParentId
        {
            get {
                if(_parentId == ID_NEEDS_RETRIEVAL) {
                    if(_parentName != null) {
                        _parentId = _dimension.GetId(_parentName);
                    }
                    else if(_dimension.IsEntity) {
                        HFM.Try("Retrieving default parent id for {0} {1}", _dimension.Name,
                                _name != null ? _name : _id.ToString(),
                                () => _dimension.HsvTreeInfo.GetDefaultParent(Id, out _parentId));
                    }
                    else {
                        _parentId = NOT_USED;
                    }
                }
                return _parentId;
            }
        }
        /// Returns the parent name for this entity
        public string ParentName
        {
            get {
                if(_parentName == null) {
                    _parentName = _dimension.GetLabel(_parentId);
                }
                return _parentName;
            }
        }
        /// The Dimension this member belongs to
        public Dimension Dimension { get { return _dimension; } }
        /// The generation of the member (below its default parent)
        public int Generation
        {
            get {
                int gen = -1;
                HFM.Try("Retrieving generation for {0}", Name,
                        () => _dimension.HsvTreeInfo.GetItemGeneration(Id, out gen));
                return gen;
            }
        }
        /// The level of the member (below its default parent)
        public int Level
        {
            get {
                int level = -1;
                HFM.Try("Retrieving level for {0}", Name,
                        () => _dimension.HsvTreeInfo.GetItemLevel(Id, out level));
                return level;
            }
        }


        /// Constructor
        protected Member(Dimension dimension)
        {
            _dimension = dimension;
        }


        /// Constructor
        public Member(Dimension dimension, string name)
        {
            _dimension = dimension;
            name = name.Trim();
            if(name == null || name.Length == 0) {
                throw new ArgumentException("Member name cannot be null or empty");
            }

            int pos;
            if((pos = name.IndexOf('.')) >= 0) {
                _parentName = name.Substring(0, pos).Trim();
                pos++;
                if(pos >= name.Length) {
                    throw new ArgumentException("No member name was specified after the .");
                }
                _name = name.Substring(pos).Trim();
            }
            else {
                _name = name;
            }
        }


        /// Constructor
        public Member(Dimension dimension, int id)
        {
            _dimension = dimension;
            _id = id;
        }


        /// Constructor
        public Member(Dimension dimension, int id, int parentId)
        {
            _dimension = dimension;
            _id = id;
            _parentId = parentId;
        }


        public bool Equals(Member m)
        {
            if(m != null) {
                return ReferenceEquals(m.Dimension, Dimension) &&
                    m.Id == Id && m.ParentId == ParentId;
            }
            return false;
        }


        public override int GetHashCode() {
            return Id.GetHashCode();
        }


        public override string ToString()
        {
            return Name;
        }

    }



    /// <summary>
    /// Represents a member of the Entity dimension.
    /// <summary>
    public class Entity : Member
    {

        /// Constructor
        public Entity(Dimension dimension, string name)
            : base(dimension, name)
        { }


        /// Constructor
        public Entity(Dimension dimension, int id, int parentId)
            : base(dimension, id, parentId)
        { }


        /// Returns the default currency id for this entity
        public int DefaultCurrencyId {
            get {
                int currId = -1;
                HFM.Try("Retrieving default currency for entity {0}", Name,
                        () => ((HsvEntities)_dimension.HsvTreeInfo).GetDefaultValueID(Id, out currId));
                return currId;
            }
        }


        public override string ToString()
        {
            return ParentId == -1 ? Name : ParentName + "." + Name;
        }

    }


    public class Account : Member
    {
        internal HsvAccounts HsvAccounts { get { return (HsvAccounts)_dimension.HsvTreeInfo; } }

        /// Constructor
        internal Account(Dimension dimension, string name)
            : base(dimension, name)
        { }

        /// Constructor
        public Account(Dimension dimension, int id)
            : base(dimension, id)
        { }


        /// Returns the top custom member for this account for the specified custom dimension
        public Member GetTopCustomMember(int customDimId)
        {
            int topCustomId = Member.NOT_USED;
            int customNum = _dimension.Metadata.CheckCustomDimId(customDimId);

            if(HFM.HasVariableCustoms) {
                HFM.Try("Retrieving top member of custom {0} hierarchy for account {1}", customNum, Name,
                        () => HsvAccounts.GetTopMemberOfValidCustomXHierarchy(customDimId, Id, out topCustomId));
            }
            else {
                switch(customDimId) {
                    case (int)EDimension.Custom1:
                        HFM.Try("Retrieving top member of custom 1 hierarchy for account {0} ", Name,
                                () => HsvAccounts.GetTopMemberOfValidCustom1Hierarchy(Id, out topCustomId));
                        break;
                    case (int)EDimension.Custom2:
                        HFM.Try("Retrieving top member of custom 2 hierarchy for account {0} ", Name,
                                () => HsvAccounts.GetTopMemberOfValidCustom2Hierarchy(Id, out topCustomId));
                        break;
                    case (int)EDimension.Custom3:
                        HFM.Try("Retrieving top member of custom 3 hierarchy for account {0} ", Name,
                                () => HsvAccounts.GetTopMemberOfValidCustom3Hierarchy(Id, out topCustomId));
                        break;
                    case (int)EDimension.Custom4:
                        HFM.Try("Retrieving top member of custom 4 hierarchy for account {0}", Name,
                                () => HsvAccounts.GetTopMemberOfValidCustom4Hierarchy(Id, out topCustomId));
                        break;
                }
            }
            return new Member(_dimension.Metadata[customDimId], topCustomId);
        }

    }


    /// <summary>
    /// A group of 1 or more members from a single dimension.
    /// </summary>
    public class MemberList : IEnumerable<Member>
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Regular expression for matching a member list specification.
        /// These must be enclosed in { and }.
        protected static Regex MEMBER_LIST_RE = new Regex(@"^\{(?:(" + Member.NAME_RE_CLASS +
                @")\.)?(" + Member.NAME_RE_CLASS + @")\}$");
        /// Regular expression for matching a member range specification.
        protected static Regex MEMBER_RANGE_RE = new Regex(@"^(" + Member.NAME_RE_CLASS +
                @")\s*(\-|\:)\s*(" + Member.NAME_RE_CLASS + ")$");
        /// Regular expression for matching a member list specification.
        protected static Regex MEMBER_RE = new Regex(@"^(?:(" + Member.NAME_RE_CLASS +
                @")\.)?(" + Member.NAME_RE_CLASS + ")$");

        /// Dimension this member set is for
        private Dimension _dimension;
        /// The member selection criteria
        private IEnumerable<string> _memberSpecs;
        /// Count of the number of specs in _memberSpecs
        private int _specCount;
        /// Member ids
        private int[] _ids;
        /// Parent ids
        private int[] _parentIds;
        /// Member objects
        private Member[] _members;


        /// Returns the n-th Member in the list
        public Member this[int i] {
            get {
                if(i < 0 && i >= Count) {
                    throw new IndexOutOfRangeException(string.Format("Invalid index {0} for MemberList; " +
                                "value must be between 0 and {1}", i, Count - 1));
                }
                if(_members == null) {
                    _members = new Member[Count];
                }
                if(_members[i] == null) {
                    _members[i] = _dimension.GetMember(_ids[i], _parentIds != null ?
                            _parentIds[i] : Member.NOT_USED);
                }
                return _members[i];
            }
        }
        /// Returns the number of members in the list
        public int Count { get { return _ids.Length; } }
        /// Returns the dimension to which this member list relates
        public Dimension Dimension { get { return _dimension; } }
        /// Returns the member ids of the members corresponding to the member
        /// specification.
        public int[] MemberIds { get { return _ids; } }
        /// Returns the parent ids of the members corresponding to the member
        /// specification.
        public int[] ParentIds { get { return _parentIds; } }


        /// <summary>
        /// Creates a member selection containing a single specification, such
        /// as {[Base]}.
        /// </summary>
        protected internal MemberList(Dimension dimension, string memberSpec)
            : this(dimension, memberSpec.Split(','))
        { }


        /// <summary>
        /// Creates a member selection from a list of member specifications.
        /// </summary>
        protected internal MemberList(Dimension dimension, IEnumerable<string> memberSpecs)
        {
            _dimension = dimension;
            _memberSpecs = memberSpecs;
            if(memberSpecs == null) {
                throw new ArgumentException("A null value cannot be used as a member specification");
            }
            _specCount = 0;
            foreach(var spec in _memberSpecs) {
                ++_specCount;
                if(MEMBER_LIST_RE.IsMatch(spec)) {
                    AddItemIdsForMemberList(spec);
                }
                else if(MEMBER_RANGE_RE.IsMatch(spec)) {
                    AddItemIdsForMemberRange(spec);
                }
                else if(MEMBER_RE.IsMatch(spec)) {
                    AddItemIdsForMember(spec);
                }
                else {
                    throw new ArgumentException(string.Format(
                                "The member specification '{0}' is not valid", spec));
                }
            }
            if(_ids == null || _ids.Length == 0) {
                throw new ArgumentException("No members were added to the member list");
            }
        }


        public IEnumerator<Member> GetEnumerator()
        {
            return IterateMembers(_ids, _parentIds).GetEnumerator();
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        public override string ToString()
        {
            return string.Join(",", _memberSpecs.ToArray());
        }


        /// Retrieves the member ids for all members in the member list
        /// specification.
        protected void AddItemIdsForMemberList(string listName)
        {
            int topId = -1, listId;
            object members = null, parents = null;

            var match = MEMBER_LIST_RE.Match(listName);
            var parent = match.Groups[1].Value;
            var list = match.Groups[2].Value;
            if(parent.Length > 0) {
                topId = _dimension.GetId(parent);
            }
            listId = _dimension.GetMemberListId(list);
            HFM.Try("Retrieving ids for member list '{0}'", listName,
                    () => _dimension.HsvTreeInfo.EnumMembers(listId, topId, out members, out parents));
            AddIds((int[])members, (int[])parents);
        }


        /// Retrieves member ids for all members in a range
        protected void AddItemIdsForMemberRange(string range)
        {
            var match = MEMBER_RANGE_RE.Match(range);
            var first = new Member(_dimension, match.Groups[1].Value);
            var useLevel = match.Groups[2].Value == "-";
            var last = new Member(_dimension, match.Groups[3].Value);
            int rangeGenLevel;

            // Find a common level or generation to filter on
            if(useLevel && first.Level != last.Level) {
                throw new ArgumentException(string.Format("Level-based range {0} is invalid; " +
                            "the start and end members of the range are on different levels", range));
            }
            else if(!useLevel && first.Generation != last.Generation) {
                throw new ArgumentException(string.Format("Generation-based range {0} is invalid; " +
                            "the start and end members of the range are on different generations", range));
            }
            rangeGenLevel = useLevel ? first.Level : first.Generation;

            // Next sort members into hierarchy order
            object oIds = null, oParentIds = null, oSortedIds = null, oSortedParentIds = null;
            HFM.Try("Retrieving member ids", () => {
                _dimension.HsvTreeInfo.EnumAllParentAndChildIDs(out oParentIds, out oIds);
                _dimension.HsvTreeInfo.SortMembersBasedOnList(Dimension.MEMBER_LIST_ALL_HIERARCHY,
                    Dimension.TREE_ROOT, true, oIds, oParentIds, out oSortedIds, out oSortedParentIds);
            });
            int[] sortedIds = (int[])oSortedIds;
            int[] sortedParentIds = (int[])oSortedParentIds;


            // Now filter the sorted list of members by those between the first
            // and last member that are also on the same level / generation
            bool inRange = false;
            int found = 0;
            var mbrs = sortedIds.Select((id, i) => new Member(_dimension, id, sortedParentIds[i]))
                                .Where(mbr => {
                                    bool include = false;
                                    if(found == 0) {
                                        if(mbr.Id == first.Id) {
                                            inRange = true;
                                        }
                                        else if(mbr.Id == last.Id) {
                                            var c = first;
                                            first = last;
                                            last = c;
                                            inRange = true;
                                        }
                                    }
                                    if(inRange) {
                                        include = useLevel ?
                                                    mbr.Level == rangeGenLevel :
                                                    mbr.Generation == rangeGenLevel;
                                        found++;
                                    }
                                    if(mbr.Id == last.Id) {
                                        inRange = false;
                                    }
                                    return include;
                                }).ToArray();

            // Finally, copy the member and parent ids
            var members = new int[mbrs.Length];
            var parents = new int[mbrs.Length];
            for(int i = 0; i < mbrs.Length; ++i) {
                members[i] = mbrs[i].Id;
                parents[i] = mbrs[i].ParentId;
            }
            AddIds(members, parents);
        }


        /// Retrieves member and parent ids for the a single member spec, such
        /// as "Mar" or "FOO.BAR".
        protected void AddItemIdsForMember(string member)
        {
            var mbr = _dimension.GetMember(member);
            AddIds(new int[] { mbr.Id }, new int[] { mbr.ParentId });
        }


        /// Appends a set of member and parent ids to the end of the current
        /// set. If no current member ids exist, this creates them.
        protected void AddIds(int[] members, int[] parents)
        {
            if(_ids == null) {
                _ids = members;
                _parentIds = parents;
            }
            else if(_parentIds == null) {
                _ids = _ids.Union(members).ToArray();
            }
            else {
                // Need to keep unique combos of member + parent
                var combined = IterateMembers(_ids, _parentIds).
                                    Union(IterateMembers(members, parents));
                _ids = combined.Select(m => m.Id).ToArray();
                _parentIds = combined.Select(m => m.ParentId).ToArray();
            }
        }


        protected IEnumerable<Member> IterateMembers(int[] members, int[] parents)
        {
            for(var i = 0; i < members.Length; ++i) {
                yield return _dimension.GetMember(members[i],
                        parents != null ? parents[i] : Member.NOT_USED);
            }
        }

    }



    /// <summary>
    /// An object for describing a single cell in an HFM application. Contains a
    /// Member object for each dimension.
    /// </summary>
    public class POV
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Reference to the Metadata object for this app
        private Metadata _metadata;
        /// An array of Members, one for each dimension in the app
        private Member[] _members;

        /// Returns the Member for the specified dimension
        public Member this[EDimension dim] {
            get { return this[(int)dim]; }
            set { this[(int)dim] = value; }
        }
        /// Returns the Member for the specified dimension number
        public Member this[int dimId]
        {
            get {
                _metadata.CheckDimId(dimId);
                return _members[dimId];
            }
            set {
                _metadata.CheckDimId(dimId);
                _members[dimId] = value;
            }
        }
        /// Returns the member of the Scenario dimension
        public Member Scenario {
            get { return this[EDimension.Scenario]; }
            set { this[EDimension.Scenario] = value; }
        }
        /// Returns the member of the Year dimension
        public Member Year {
            get { return this[EDimension.Year]; }
            set { this[EDimension.Year] = value; }
        }
        /// Returns the member of the Period dimension
        public Member Period {
            get { return this[EDimension.Period]; }
            set { this[EDimension.Period] = value; }
        }
        /// Returns the member of the View dimension
        public Member View {
            get { return this[EDimension.View]; }
            set { this[EDimension.View] = value; }
        }
        /// Returns the member of the Entity dimension
        public Member Entity {
            get { return this[EDimension.Entity]; }
            set { this[EDimension.Entity] = value; }
        }
        /// Returns the member of the Value dimension
        public Member Value {
            get { return this[EDimension.Value]; }
            set { this[EDimension.Value] = value; }
        }
        /// Returns the member of the Account dimension
        public Member Account {
            get { return this[EDimension.Account]; }
            set { this[EDimension.Account] = value; }
        }
        /// Returns the member of the ICP dimension
        public Member ICP {
            get { return this[EDimension.ICP]; }
            set { this[EDimension.ICP] = value; }
        }
        /// Returns the member of the Custom1 dimension
        public Member Custom1 {
            get { return this[EDimension.Custom1]; }
            set { this[EDimension.Custom1] = value; }
        }
        /// Returns the member of the Custom2 dimension
        public Member Custom2 {
            get { return this[EDimension.Custom2]; }
            set { this[EDimension.Custom2] = value; }
        }
        /// Returns the member of the Custom3 dimension
        public Member Custom3 {
            get { return this[EDimension.Custom3]; }
            set { this[EDimension.Custom3] = value; }
        }
        /// Returns the member of the Custom4 dimension
        public Member Custom4 {
            get { return this[EDimension.Custom4]; }
            set { this[EDimension.Custom4] = value; }
        }
        /// Converts this POV to an HfmPovCOM object
        public HfmPovCOM HfmPovCOM
        {
            get {
                Member member;
                var customIds = new int[_metadata.NumberOfCustomDims];
                var pov = new HfmPovCOM();
                for(int i = 0, c = 0; i < _members.Length; ++i) {
                    member = _members[i];
                    if(member != null) {
                        c = i - (int)EDimension.CustomBase;
                        if(c < 0) {
                            pov.SetDimensionMember(member.Dimension.Id, member.Id);
                            if(member.Dimension.IsEntity) {
                                pov.SetDimensionMember((int)tagHFMDIMENSIONS2.DIMID_PARENT, member.ParentId);
                            }
                        }
                        else {
                            customIds[c] = member.Id;
                        }
                    }
                }
                pov.SetCustoms(_metadata.CustomDimIds, customIds);
                return pov;
            }
        }
        /// Converts this POV to an HfmSliceCOM object
        public HfmSliceCOM HfmSliceCOM
        {
            get {
                var slice = new HfmSliceCOM();
                foreach(var member in _members) {
                    if(member != null) {
                        slice.SetFixedMember(member.Dimension.Id, member.Id);
                        if(member.Dimension.IsEntity) {
                            slice.SetFixedMember((int)tagHFMDIMENSIONS2.DIMID_PARENT, member.ParentId);
                        }
                    }
                }
                return slice;
            }
        }


        /// Constructor
        public POV(Metadata metadata) {
            _metadata = metadata;
            _members = new Member[_metadata.NumberOfDims];
        }


        public POV Copy() {
            var pov = new POV(_metadata);
            Array.Copy(_members, pov._members, _members.Length);
            return pov;
        }


        public override string ToString() {
            return ToString(_metadata.DimensionIds);
        }


        public string ToString(params EDimension[] dimensions)
        {
            return ToString(Array.ConvertAll(dimensions, (d) => (int)d));
        }


        public string ToString(params int[] dimensions)
        {
            var sb = new StringBuilder();
            foreach(int id in dimensions) {
                if(this[id] == null || this[id].Id == Member.NOT_USED) { continue; }
                if(_metadata.DimensionNames[id] == "View") {
                    sb.Append('W');
                }
                else if(id < (int)EDimension.CustomBase) {
                    sb.Append(_metadata.DimensionNames[id][0]);
                }
                else {
                    sb.Append(_metadata.DimensionNames[id]);
                }
                sb.Append('#');
                if(id == (int)EDimension.Entity) {
                    sb.Append(this[id].ParentName);
                    sb.Append('.');
                }
                sb.Append(this[id].Name);
                sb.Append('.');
            }
            sb.Length--;
            return sb.ToString();
        }

    }



    /// <summary>
    /// Exception thrown when an attempt is made to use an incomplete Slice or
    /// POV object.
    /// </summary>
    public class IncompleteSliceDefinition : Exception
    {
        public IncompleteSliceDefinition(string msg) : base(msg) { }
    }



    /// <summary>
    /// A Slice represents a set of member selections for each dimension in an
    /// HFM application. The slice can then be used to obtain member ids for
    /// each cell that intersects the slice definition.
    /// </summary>
    public class Slice
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Reference to the Metadata object for the app
        private Metadata _metadata;
        /// The POV specification used to initialise the Slice
        private string _pov;
        /// An array of MemberList objects, one per dimension
        private MemberList[] _memberLists;


        /// Gets or sets the MemberList object for the specified dimension.
        public object this[string dimension]
        {
            get {
                if(dimension == "POV") {
                    return _pov;
                }
                int id = _metadata.GetDimensionId(ref dimension);
                if(_memberLists[id] == null) {
                    throw new IncompleteSliceDefinition(string.Format(
                                "No members have been specified for the {0} dimension", dimension));
                }
                return _memberLists[id];
            }
            set {
                if(dimension == "POV") {
                    MergePOV(value as string);
                }
                else {
                    int id = _metadata.GetDimensionId(ref dimension);
                    this[id] = value;
                }
            }
        }
        /// Sets a member list for the specified dimension id
        internal object this[EDimension dim] { set { this[(int)dim] = value; } }
        /// Sets a member list for the specified dimension id
        internal object this[int dimId]
        {
            get {
                _metadata.CheckDimId(dimId);
                return _memberLists[dimId];
            }
            set {
                _metadata.CheckDimId(dimId);
                if(value is MemberList) {
                    var ml = value as MemberList;
                    if(dimId == ml.Dimension.Id) {
                        _memberLists[dimId] = value as MemberList;
                    }
                    else {
                        throw new ArgumentException(string.Format(
                                    "Member list for {0} cannot be set using a member list for {1}",
                                    (EDimension)dimId, ml.Dimension));
                    }
                }
                else if(value is string) {
                    _memberLists[dimId] = new MemberList(_metadata[dimId], value as string);
                }
                else {
                    throw new ArgumentException(string.Format("Invalid type for Slice dimension {0}",
                                _metadata[dimId].Name));
                }
            }
        }
        /// Returns the names of the custom dimensions; useful for sub-classes
        /// use a Slice sub-class as a parameter to a command
        public string[] DynamicSettingNames
        {
            get {
                int numCustoms = _metadata.NumberOfCustomDims;
                string[] names = new string[numCustoms * 2];
                Array.Copy(_metadata.CustomDimNames, names, numCustoms);
                Array.Copy(_metadata.CustomDimAliases, 0, names, numCustoms, numCustoms);
                return names;
            }
        }
        /// Returns the member list for the Scenario dimension
        public MemberList Scenarios {
            get { return MemberList(EDimension.Scenario); }
            set { this[EDimension.Scenario] = value; }
        }
        /// Returns the member list for the Year dimension
        public MemberList Years {
            get { return MemberList(EDimension.Year); }
            set { this[EDimension.Year] = value; }
        }
        /// Returns the member list for the Period dimension
        public MemberList Periods {
            get { return MemberList(EDimension.Period); }
            set { this[EDimension.Period] = value; }
        }
        /// Returns the member list for the View dimension
        public MemberList Views {
            get { return MemberList(EDimension.View); }
            set { this[EDimension.View] = value; }
        }
        /// Returns the member list for the Entity dimension
        public MemberList Entities {
            get { return MemberList(EDimension.Entity); }
            set { this[EDimension.Entity] = value; }
        }
        /// Returns the member list for the Value dimension
        public MemberList Values {
            get { return MemberList(EDimension.Value); }
            set { this[EDimension.Value] = value; }
        }
        /// Returns the member list for the Account dimension
        public MemberList Accounts {
            get { return MemberList(EDimension.Account); }
            set { this[EDimension.Account] = value; }
        }
        /// Returns the member list for the ICP dimension
        public MemberList ICPs {
            get { return MemberList(EDimension.ICP); }
            set { this[EDimension.ICP] = value; }
        }
        /// Returns the member list for the Custom1 dimension
        public MemberList Custom1 {
            get { return MemberList(EDimension.Custom1); }
            set { this[EDimension.Custom1] = value; }
        }
        /// Returns the member list for the Custom2 dimension
        public MemberList Custom2 {
            get { return MemberList(EDimension.Custom2); }
            set { this[EDimension.Custom2] = value; }
        }
        /// Returns the member list for the Custom3 dimension
        public MemberList Custom3 {
            get { return MemberList(EDimension.Custom3); }
            set { this[EDimension.Custom3] = value; }
        }
        /// Returns the member list for the Custom4 dimension
        public MemberList Custom4 {
            get { return MemberList(EDimension.Custom4); }
            set { this[EDimension.Custom4] = value; }
        }
        /// Returns a count of the number of dimensions that have been specified
        public int DimensionsSpecified
        {
            get {
                return _memberLists.Count(ml => ml != null);
            }
        }
        /// Returns a count of the number of cells currently specified in the slice
        public int CellCount
        {
            get {
                int size = 0;
                for(var i = 0; i < _memberLists.Length; ++i) {
                    if(_memberLists[i] != null) {
                        size = (size > 0 ? size : 1) * _memberLists[i].Count;
                    }
                }
                return size;
            }
        }
        /// Returns an array of all cells in the slice
        public POV[] POVs { get { return GeneratePOVs(); } }
        /// Converts this Slice to an HfmSliceCOM object
        public HfmSliceCOM HfmSliceCOM
        {
            get {
                var slice = new HfmSliceCOM();
                foreach(var ml in _memberLists) {
                    if(ml != null) {
                        if(ml.Count > 1) {
                            slice.SetMemberArrayForDim(ml.Dimension.Id, ml.MemberIds);
                            if(ml.Dimension.IsEntity) {
                                slice.SetMemberArrayForDim((int)tagHFMDIMENSIONS2.DIMID_PARENT, ml.ParentIds);
                            }
                        }
                        else if(ml.Count == 1) {
                            slice.SetFixedMember(ml.Dimension.Id, ml.MemberIds[0]);
                            if(ml.Dimension.IsEntity) {
                                slice.SetFixedMember((int)tagHFMDIMENSIONS2.DIMID_PARENT, ml.ParentIds[0]);
                            }
                        }
                    }
                }
                return slice;
            }
        }


        /// <summary>
        /// Returns the MemberList that defines the Slice member(s) for the
        /// specified dimension
        /// </summary>
        public MemberList MemberList(string dimension)
        {
            return this[dimension] as MemberList;
        }


        /// Returns a MemberList for the specified dimension
        internal MemberList MemberList(EDimension dim)
        {
            return MemberList((int)dim);
        }


        /// Returns a MemberList for the dimension with the specified id
        internal MemberList MemberList(int dimId)
        {
            _metadata.CheckDimId(dimId);
            if(_memberLists[dimId] == null) {
                throw new IncompleteSliceDefinition(string.Format(
                            "No members have been specified for the {0} dimension",
                            _metadata.DimensionNames[dimId]));
            }
            return _memberLists[dimId];
        }


        internal bool IsSpecified(EDimension dim)
        {
            return IsSpecified((int)dim);
        }


        internal bool IsSpecified(int dimId)
        {
            _metadata.CheckDimId(dimId);
            return _memberLists[dimId] != null;
        }


        /// Constructor
        public Slice(Metadata metadata)
        {
            _metadata = metadata;
            _memberLists = new MemberList[_metadata.NumberOfDims];
        }


        /// Constructor
        public Slice(Metadata metadata, string pov)
            : this(metadata)
        {
            MergePOV(pov);
        }


        /// Merges a POV specification into the current Slice definition.
        /// A POV specification consists of one or more dimension components,
        /// separated by either periods or semi-colons. Each dimension component
        /// may be one of:
        /// - a dimension name or letter, followed by # and then a member name,
        ///   such as Period#May, Value#<Entity Currency>, S#Actual, Y#2010, etc
        /// - a dimension name or letter, followed by one or more comma-separated
        ///   member lists or member ranges, such as P{[Fourth Generation]},
        ///   ICP{[Base]} etc
        protected void MergePOV(string pov)
        {
            int pos;
            string dimName, mbrSpec;

            _pov = pov;
            var mbrs = pov.Split(new char[] { '.', ';' });
            foreach(var mbr in mbrs) {
                if((pos = mbr.IndexOf('#')) > 0) {
                    dimName = mbr.Substring(0, pos);
                    mbrSpec = mbr.Substring(pos + 1);
                }
                else if((pos = mbr.IndexOf('{')) > 0) {
                    dimName = mbr.Substring(0, pos);
                    mbrSpec = mbr.Substring(pos);
                }
                else {
                    throw new ArgumentException(string.Format("Unable to parse dimension specification '{0}'", mbr));
                }
                var dimension = _metadata[dimName];
                if(_memberLists[dimension.Id] == null) {
                    _memberLists[dimension.Id] = new MemberList(dimension, mbrSpec);
                }
                else {
                    _log.DebugFormat("Skipping POV dimension {0}; a member list has already been defined",
                            dimension.Name);
                }
            }
        }


        // Returns an array of POV objects representing each cell in the Slice.
        protected POV[] GeneratePOVs()
        {
            _log.Trace("Generating POV array");
            int numDims = _metadata.NumberOfDims;
            if(numDims - DimensionsSpecified > 1) {
                // Multiple dimensions have not been specified
                throw new IncompleteSliceDefinition(string.Format(
                            "No members have been specified for the following dimensions: {0}",
                            string.Join(", ", _metadata.DimensionNames.
                                Where((name, i) => _memberLists[i] == null).ToArray())));
            }
            else if(_memberLists[(int)EDimension.View] == null) {
                // Default view to <Scenario View>
                this["View"] = "<Scenario View>";
            }
            return GenerateCombos(_memberLists);
        }


        // Returns an array of POV objects for each combination of the dimensions in lists
        protected POV[] GenerateCombos(MemberList[] lists)
        {
            int numDims = lists.Length;
            int[] dimIds = new int[numDims];
            int[] dimSizes = new int[numDims];
            int[] dimIdx = new int[numDims];
            int size = 1;
            int dim = 0;
            foreach(var list in lists) {
                dimIds[dim] = list.Dimension.Id;
                dimSizes[dim] = list.Count;
                size = size * dimSizes[dim];
                dim++;
            }
            _log.DebugFormat("Number of combinations in slice: {0}", size);

            var povs = new POV[size];
            POV pov;
            for(int id, i = 0; i < size; ++i) {
                pov = new POV(_metadata);
                for(dim = 0; dim < numDims; ++dim) {
                    id = dimIds[dim];
                    pov[id] = lists[dim][dimIdx[dim]];
                }
                povs[i] = pov;

                // Update member list indexers
                for(dim = 0; dim < numDims; ++dim) {
                    if(++dimIdx[dim] < dimSizes[dim]) {
                        break;
                    }
                    else {
                        dimIdx[dim] = 0;
                    }
                }
            }
            return povs;
        }


        public override string ToString() {
            return ToString(_metadata.DimensionIds);
        }


        public string ToString(params EDimension[] dimensions)
        {
            return ToString(Array.ConvertAll(dimensions, (d) => (int)d));
        }


        public string ToString(params int[] dimensions)
        {
            var sb = new StringBuilder();
            foreach(int id in dimensions) {
                if(MemberList(id) == null) { continue; }
                if(_metadata.DimensionNames[id] == "View") {
                    sb.Append('W');
                }
                else if(id < (int)EDimension.CustomBase) {
                    sb.Append(_metadata.DimensionNames[id][0]);
                }
                else {
                    sb.Append(_metadata.DimensionNames[id]);
                }
                if(MemberList(id).Count == 1) {
                    sb.Append('#');
                }
                sb.Append(MemberList(id).ToString());
                sb.Append('.');
            }
            sb.Length--;
            return sb.ToString();
        }
    }

}
