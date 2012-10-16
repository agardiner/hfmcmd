using System;
using System.Collections.Generic;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALib;

using Command;


namespace HFM
{

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


        [Factory]
        public Metadata(HsvSession session)
        {
            _hsvMetadata = (HsvMetadata)session.Metadata;
        }

    }



    /// <summary>
    /// Represents an HFM dimension, and contains functionality for obtaining
    /// members, enumerating lists, etc.
    /// <summary>
    public class Dimension
    {
        protected IHsvTreeInfo _hsvTreeInfo;


        public Dimension(IHsvTreeInfo treeInfo)
        {
            _hsvTreeInfo = treeInfo;
        }

    }



    /// <summary>
    /// Represents a member of a dimension (other than Entity).
    /// <summary>
    public abstract class Member
    {
        protected Dimension _dimension;
        protected string _name;
        protected int _id = -2;

        /// The internal id for the member
        public int Id { get { return _id; } }
        /// The name of the member
        public string Name { get { return _name; } }


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
            _name = name;
        }
    }



    /// <summary>
    /// Represents a member of the Entity dimension.
    /// <summary>
    public class Entity : Member
    {
        protected string _parentName = null;
        protected int _parentId = -2;
        protected int _defaultCurrencyId = -1;

        public int ParentId { get { return _parentId; } }
        public string ParentName { get { return _parentName; } }
        public int DefaultCurrencyId { get { return _defaultCurrencyId; } }


        public Entity(Dimension dimension, string name)
            : base(dimension)
        {
            int pos;

            name = name.Trim();
            if(name == null || name.Length == 0) {
                throw new ArgumentException("Entity member name cannot be null or empty");
            }

            if((pos = name.IndexOf('.')) >= 0) {
                _parentName = name.Substring(0, pos).Trim();
                pos++;
                if(pos >= name.Length) {
                    throw new ArgumentException("Entity name must be specified after the .");
                }
                _name = name.Substring(pos).Trim();
            }
            else {
                _name = name;
            }
        }
    }
}
