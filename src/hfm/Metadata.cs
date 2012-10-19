using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALib;

using Command;
using HFMCmd;


namespace HFM
{

    public enum ECustomDimSize : short
    {
        Small = 1,
        Medium = 2,
        Large = 4
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


        // Reference to HFM HsvMetadata object
        protected readonly HsvMetadata _hsvMetadata;
        // Cache of dimensions
        protected Dictionary<string, Dimension> _dimensions =
            new Dictionary<string, Dimension>(StringComparer.OrdinalIgnoreCase);


        /// Returns the Dimension object for the specified dimension
        public Dimension this[string dimName]
        {
            get {
                if(!_dimensions.ContainsKey(dimName)) {
                    int dimId = -1;
                    IHsvTreeInfo dim = null;

                    // TODO: Determine canonical name of dimension

                    HFM.Try("Retrieving dimension id for {0}", dimName,
                            () => _hsvMetadata.GetDimensionIdFromName(dimName, out dimId));
                    HFM.Try("Retrieving dimension {0}", dimName,
                            () => dim = (IHsvTreeInfo)_hsvMetadata.GetDimension(dimId));
                    _dimensions[dimName] = new Dimension(dimName, dim);
                }
                return _dimensions[dimName];
            }
        }



        [Factory]
        public Metadata(Session session)
        {
            _hsvMetadata = (HsvMetadata)session.HsvSession.Metadata;
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


        [Command("Lists the valid custom dimensions for an application",
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


        [Command("Returns all members of a dimension")]
        public string[] EnumMembers(
            [Parameter("The dimension whose members are to be returned")]
            string dimension,
            IOutput output)
        {
            return this[dimension].EnumAllMembers(output);
        }


        [Command("Returns all member lists for a dimension")]
        public string[] EnumMemberLists(
            [Parameter("The dimension whose member lists are to be returned")]
            string dimension,
            IOutput output)
        {
            return this[dimension].EnumMemberLists(output);
        }


        [Command("Returns all members in a member list")]
        public void EnumMembersInList(
            [Parameter("The dimension whose member lists are to be returned")]
            string dimension,
            [Parameter("The member list whose members are to be returned; " +
                       "should be enclosed in { and }, e.g. {[Base]}")]
            string memberList,
            IOutput output)
        {
            var members = this[dimension].GetMembers(memberList);
            output.WriteEnumerable(members, "Member");
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

        protected IHsvTreeInfo _hsvTreeInfo;
        protected string _name;

        public const int MEMBER_LIST_ALL_HIERARCHY = 0;
        public const int TREE_ROOT = -1;

        // Properties

        /// Internal access to the underlying HsvTreeInfo COM object
        internal IHsvTreeInfo HsvTreeInfo { get { return _hsvTreeInfo; } }
        /// The name of the dimension
        public string Name { get { return _name; } }
        /// True if this is the Entity dimension
        public bool IsEntity { get { return _name == "Entity"; } }


        /// Constructor
        public Dimension(string dimension, IHsvTreeInfo treeInfo)
        {
            _name = dimension;
            _hsvTreeInfo = treeInfo;
        }


        /// <summary>
        /// Returns the member id for a member with the specified label.
        /// </summary>
        public int GetId(string member)
        {
            int id = -1;

            HFM.Try("Retrieving member id for {0}", member,
                    () => id = HsvTreeInfo.GetItemID(member));

            if (id >= 0) {
                return id;
            }
            else {
                throw new ArgumentException("No member named '" + member + "' exists in dimension " + _name);
            }
        }


        /// <summary>
        /// Returns the member label for a member with the specified id.
        /// </summary>
        public string GetLabel(int id)
        {
            string label = null;

            HFM.Try("Retrieving member label for {0}", id,
                    () => id = HsvTreeInfo.GetLabel(id, out label));

            return label;
        }


        /// <summary>
        /// Returns the member list id for the specified member list name.
        /// </summary>
        public int GetMemberListId(string listName)
        {
            int id = -1;

            HFM.Try("Retrieving id for member list {0}", listName,
                    () => HsvTreeInfo.GetMemberListID(listName, out id));

            if (id >= 0) {
                return id;
            }
            else {
                throw new ArgumentException("No member list named '" + listName +
                        "' exists in dimension " + _name);
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
        /// Returns a collection of members corresponding to the member specs.
        /// </summary>
        public MemberList GetMembers(string memberSpec)
        {
            return new MemberList(this, memberSpec);
        }


        /// <summary>
        /// Returns a collection of members corresponding to the member specs.
        /// </summary>
        public MemberList GetMembers(IEnumerable<string> memberSpecs)
        {
            return new MemberList(this, memberSpecs);
        }


        public Member CreateMember(int id)
        {
            return CreateMember(id, Member.ID_NOT_SPECIFIED);
        }

        public Member CreateMember(int id, int parentId)
        {
            if(IsEntity) {
                return new Entity(this, id, parentId);
            }
            else {
                return new Member(this, id, parentId);
            }
        }

    }


    /*
    public class AccountsDimension : Dimension { }
    public class EntityDimension : Dimension { }
    public class CustomDimension : Dimension { }
    */


    /// <summary>
    /// Represents a member of a dimension (other than Entity).
    /// <summary>
    public class Member
    {
        public const int ID_NOT_SPECIFIED = -2;

        protected Dimension _dimension;
        protected string _name;
        protected int _id = ID_NOT_SPECIFIED;
        protected string _parentName = null;
        protected int _parentId = ID_NOT_SPECIFIED;

        /// The internal id for the member
        public int Id
        {
            get {
                if(_id == ID_NOT_SPECIFIED) {
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
        /// Returns the parent id for this entity
        public int ParentId
        {
            get {
                if(_parentId == ID_NOT_SPECIFIED) {
                    if(_parentName != null) {
                        _parentId = _dimension.GetId(_parentName);
                    }
                    else {
                        HFM.Try("Retrieving default parent id",
                                () => _dimension.HsvTreeInfo.GetDefaultParent(Id, out _parentId));
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
        /// The position of the member in the default hierarchy (as per the
        /// [Hierarchy] tree representation)
        public int DefaultHierarchyPosition
        {
            get {
                int pos = -1;
                HFM.Try("Retrieving default hierarchy position for {0}", Name,
                        () => _dimension.HsvTreeInfo.GetDefaultHierarchyPosition(Id, out pos));
                return pos;
            }
        }
        /// The generation of the member (below its default parent)
        public int Generation
        {
            get {
                int gen = -1;
                HFM.Try("Retrieving generation for {0}", Name,
                        () => _dimension.HsvTreeInfo.GetItemLevel(Id, out gen));
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


        protected Member(Dimension dimension)
        {
            _dimension = dimension;
        }


        public Member(Dimension dimension, string name)
        {
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


        public Member(Dimension dimension, int id)
        {
            _dimension = dimension;
            _id = id;
        }


        public Member(Dimension dimension, int id, int parentId)
        {
            _dimension = dimension;
            _id = id;
            _parentId = parentId;
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
        /// Returns the default currency id for this entity
        public int DefaultCurrencyId {
            get {
                int currId = -1;
                HFM.Try("Retrieving default currency",
                        () => ((HsvEntities)_dimension.HsvTreeInfo).GetDefaultValueID(Id, out currId));
                return currId;
            }
        }


        public Entity(Dimension dimension, string name)
            : base(dimension, name)
        { }


        public Entity(Dimension dimension, int id, int parentId)
            : base(dimension, id, parentId)
        { }


        public override string ToString()
        {
            return ParentId == -1 ? Name : ParentName + "." + Name;
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

        // TODO: Make these regexes more correct
        /// Regular expression for matching a member list specification.
        /// These must be enclosed in { and }.
        protected static Regex MEMBER_LIST_RE = new Regex(@"^\{(?:([^.]+)\.)?([^.]+)\}$");
        /// Regular expression for matching a member range specification.
        protected static Regex MEMBER_RANGE_RE = new Regex(@"^([^-.\[\]{}]+)\s*\-\s*([^-.\[\]{}]+)$");
        /// Regular expression for matching a member list specification.
        protected static Regex MEMBER_RE = new Regex(@"^(?:([^.{}]+)\.)?([^.{}]+)$");

        /// Dimension this member set is for
        Dimension _dimension;
        /// The member selection criteria
        IEnumerable<string> _memberSpecs;
        /// Count of the number of specs in _memberSpecs
        int _specCount;
        /// Member ids
        int[] _members;
        /// Parent ids
        int[] _parents;


        /// Returns the number of members in the list
        public int Count { get { return _members.Length; } }
        /// Returns the dimension to which this member list relates
        public Dimension Dimension { get { return _dimension; } }
        /// Returns the member ids of the members corresponding to the member
        /// specification.
        public int[] MemberIds { get { return _members; } }
        /// Returns the parent ids of the members corresponding to the member
        /// specification.
        public int[] ParentIds { get { return _parents; } }
        /// Returns the first member in the list
        public Member FirstMember
        {
            get {
                return _dimension.CreateMember(_members[0],
                            _parents != null ? _parents[0] : Member.ID_NOT_SPECIFIED);
            }
        }
        /// Returns the last member in the list
        public Member LastMember
        {
            get {
                if(_specCount > 1) {
                    throw new InvalidOperationException("LastMember cannot be called on ranges cannot be combined with other member selections");
                }
                else {
                    var last = _members.Length - 1;
                    if(_dimension.IsEntity) {
                        return new Entity(_dimension, _members[last], _parents[last]);
                    }
                    else {
                        return new Member(_dimension, _members[last]);
                    }
                }
            }
        }


        /// <summary>
        /// Creates a member selection containing a single specification, such
        /// as {[Base]}.
        /// </summary>
        protected internal MemberList(Dimension dimension, string memberSpec)
            : this(dimension, new string[] { memberSpec })
        { }


        /// <summary>
        /// Creates a member selection from a list of member specifications.
        /// </summary>
        protected internal MemberList(Dimension dimension, IEnumerable<string> memberSpecs)
        {
            bool rangeUsed = false;

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
                    rangeUsed = true;
                }
                else if(MEMBER_RE.IsMatch(spec)) {
                    AddItemIdsForMember(spec);
                }
                else {
                    throw new ArgumentException(string.Format(
                                "The member specification '{0}' is not valid", spec));
                }
            }
            if(rangeUsed && _specCount > 1) {
                throw new InvalidOperationException("Member ranges cannot be combined with other member selections");
            }
        }


        public IEnumerator<Member> GetEnumerator()
        {
            for(var i = 0; i < _members.Length; ++i) {
                yield return _dimension.CreateMember(_members[i],
                        _parents != null ? _parents[i] : Member.ID_NOT_SPECIFIED);
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
            var last = new Member(_dimension, match.Groups[2].Value);
            var firstPos = first.DefaultHierarchyPosition;
            var lastPos = last.DefaultHierarchyPosition;
            bool useLevel;
            int rangeGenLevel;

            // Find the first and last members
            if(lastPos < firstPos) {
                throw new ArgumentException(string.Format("Range {0} is invalid; try swapping the order of the members", range));
            }
            if(first.Level != last.Level && first.Generation == last.Generation) {
                throw new ArgumentException(string.Format("Range {0} is invalid; the members at the " +
                            "start and end of the range must be of the same level or generation", range));
            }
            else {
                useLevel = first.Level == last.Level;
                rangeGenLevel = useLevel ? first.Level : first.Generation;
            }

            // Next sort members into hierarchy order
            object oIds = null, oParentIds = null, oSortedIds = null, oSortedParentIds = null;
            HFM.Try("Retrieving member ids", () => {
                _dimension.HsvTreeInfo.EnumAllParentAndChildIDs(out oIds, out oParentIds);
                _dimension.HsvTreeInfo.SortMembersBasedOnList(Dimension.MEMBER_LIST_ALL_HIERARCHY,
                    Dimension.TREE_ROOT, true, oIds, oParentIds, out oSortedIds, out oSortedParentIds);
            });
            int[] sortedIds = (int[])oSortedIds;
            int[] sortedParentIds = (int[])oSortedParentIds;


            // Now filter the sorted list of members by those between the first
            // and last member that are also on the same level / generation
            bool inRange = false;
            var mbrs = sortedIds.Select((id, i) => new Member(_dimension, id, sortedParentIds[i]))
                                .Where(mbr => {
                                    bool include = false;
                                    if(mbr.Id == first.Id) {
                                        inRange = true;
                                    }
                                    if(inRange) {
                                        include = useLevel ?
                                                    mbr.Level == rangeGenLevel :
                                                    mbr.Generation == rangeGenLevel;
                                    }
                                    if(mbr.Id == last.Id) {
                                        inRange = false;
                                    }
                                    return include;
                                }).ToArray();

            // Finally, copy the member and parent ids
            _members = new int[mbrs.Length];
            _parents = new int[mbrs.Length];
            for(int i = 0; i < mbrs.Length; ++i) {
                _members[i] = mbrs[i].Id;
                _parents[i] = mbrs[i].ParentId;
            }
        }


        /// Retrieves member and parent ids for the a single member spec, such
        /// as "Mar" or "FOO.BAR".
        protected void AddItemIdsForMember(string member)
        {
            string parent = null;
            int topId = -1;
            int id = -1;

            if(member.Contains(".") | _dimension.IsEntity) {
                // Member has or requires a parent node
                int pos = member.IndexOf(".");
                if(pos >= 0) {
                    // A parent was specified
                    parent = member.Substring(0, pos);
                    member = member.Substring(pos + 1);
                    id = _dimension.GetId(member);
                    topId = _dimension.GetId(parent);
                }
                else {
                    id = _dimension.GetId(member);
                    if(_dimension.IsEntity) {
                        // No parent specified, but parent required, so find default
                        HFM.Try("Retrieving default parent",
                                () => _dimension.HsvTreeInfo.GetDefaultParent(id, out topId));
                    }
                }
                AddIds(new int[] { id }, new int[] { topId });
            }
            else {
                id = _dimension.GetId(member);
                AddIds(new int[] { id }, null);
            }
        }


        /// Appends a set of member and parent ids to the end of the current
        /// set. If no current member ids exist, this creates them.
        protected void AddIds(int[] members, int[] parents)
        {
            if(_members == null) {
                _members = members;
                _parents = parents;
            }
            else {
                int oldLen = _members.Length;
                int newLen = oldLen + members.Length;

                Array.Resize<int>(ref _members, newLen);
                Array.ConstrainedCopy(members, 0, _members, oldLen, members.Length);

                if(parents != null) {
                    System.Array.Resize<int>(ref _parents, newLen);
                    if(parents != null) {
                        Array.ConstrainedCopy(parents, 0, _parents, oldLen, parents.Length);
                    }
                }
            }
        }

    }
}
