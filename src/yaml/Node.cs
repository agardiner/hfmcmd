using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using log4net;


namespace YAML
{

    /// <summary>
    /// A Node is an item in a YAML document that is an order collection of
    /// objects, each of which is at the same nesting level. The contents of
    /// a node may be accessed either by position or by key, depending on
    /// whether the node represents a list or a collection.
    /// </summary>
    public class Node : IEnumerable<Node>
    {

        /// <summary>
        /// Represents an exception due to incorrectly specifying parent/child
        /// relationships.
        /// </summary>
        public class NestingException : Exception
        {
            public NestingException(string msg)
                : base(msg)
            { }
        }

        public class MixedContentException : Exception
        {
            public MixedContentException()
                : base("Attempt to mix keyed and non-keyed content in the same node")
            { }


            public MixedContentException(string msg)
                : base(msg)
            { }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// The internal List object used to hold the children of this Node.
        protected List<Node> _children;
        /// Flag indicating whether node is keyed or not
        protected bool _isKeyed = false;
        /// Value of this leaf node
        protected object _value = null;

        /// The key of this Node in it's parent collection (if any)
        public readonly string Key;
        /// The value of this leaf Node
        public object Value {
            get {
                return _value;
            }
            set {
                if(_children != null) {
                    throw new MixedContentException("Invalid attempt to set a value on a node with children");
                }
                _value = value;
            }
        }
        /// True if the node has a value, rather than children
        public bool IsLeaf { get { return _children == null; } }
        /// True if the node is a keyed collection
        public bool IsDictionary { get { return _children != null && _isKeyed; } }
        /// True if the node is a list collection
        public bool IsList { get { return _children != null && !_isKeyed; } }


        /// Constructs a non-keyed node that will contain children
        public Node()
        {
        }


        /// Constructs a non-collection node with the specified value
        public Node(object value)
        {
            Value = value;
        }


        /// Constructs a keyed node.
        public Node(string key, object value)
        {
            Key = key;
            if(value is Node) {
                Add(value as Node);
            }
            else {
                Value = value;
            }
        }


        /// <summary>
        /// Adds a child object to this node, identifiable/retrievable via the
        /// specified key.
        /// </summary>
        public Node Add(Node child)
        {
            if(Value != null) {
                throw new MixedContentException("Invalid attempt to add a child to a node that already " +
                                                "has a (non-collection) value");
            }
            if(_children == null) {
                _children = new List<Node>();
                _isKeyed = child.Key != null;
            }
            else if((child.Key == null && _isKeyed) ||
               (child.Key != null && !_isKeyed)) {
                throw new MixedContentException();
            }
            _children.Add(child);
            return child;
        }


        /// <summary>
        /// Returns a count of the number of children of this Node.
        /// </summary>
        public int Count
        {
            get { return _children != null ? _children.Count : 0; }
        }


        /// <summary>
        /// True if this Node contains an object with the specified key.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return Find(key).Key != null;
        }


        /// <summary>
        /// Indexed accessor used to retrieve an object by its key.
        /// If multiple objects exist with the same key, the last object with
        /// the key is returned.
        /// </summary>
        /// <param name="key">A string identifying the value to get/set.</param>
        /// <returns>The object at the specified key.</returns>
        public object this[string key]
        {
            get {
                if(key == null) {
                    throw new ArgumentException("Cannot retrieve a child value using a null key");
                }

                Node child = Find(key);
                if(child == null) {
                    throw new KeyNotFoundException("Node does not contain a child node with key '" + key + "'");
                }
                return child.Value;
            }
        }


        /// <summary>
        /// Returns the value of the item at the specified position in the
        /// list.
        /// </summary>
        /// <param name="index">The index (0-based) of the item to retrieve.
        /// </param>
        /// <returns>The value at that position in the child list.</returns>
        public object this[int index]
        {
            get {
                return _children[index].Value;
            }
        }


        /// <summary>
        /// Returns the child node identified by the specified key (if any).
        /// </summary>
        public Node GetChildNode(string key)
        {
            return Find(key);
        }


        /// <summary>
        /// Scans the child nodes, searching for the last node with the given key.
        /// </summary>
        protected Node Find(string key)
        {
            return _children == null ? null :
                _children.LastOrDefault(n => n.Key.ToUpper() == key.ToUpper());
        }


        /// <summary>
        /// Returns an enumerator for iterating over the children of this node.
        /// </summary>
        public IEnumerator<Node> GetEnumerator()
        {
            return _children.GetEnumerator();
        }


        /// <summary>
        /// Returns an enumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        /// <summary>
        /// Returns a string representation of this Node's children
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToString(true, 0);
        }


        public string ToString(bool isRoot, int indentation)
        {
            var inline = indentation < 0;
            var sb = new StringBuilder();
            var commas = 0;

            if(!isRoot && indentation >= 0) {
                sb.AppendLine();
                sb.Append(' ', indentation);
            }
            if(Key != null) {
                sb.Append(Key);
                sb.Append(": ");
            }
            else if(indentation > 0) {
                sb.Append("- ");
            }

            if(IsLeaf) {
                sb.Append(Value);
            }
            else {
                if(inline) {
                    // In-line collection
                    if (_children.Count > 0) {
                        commas = _children.Count - 1;
                    }
                    sb.Append(IsDictionary ? "{ " : "[ ");
                }

                foreach (var node in _children) {
                    sb.Append(node.ToString(false, inline ? -1 : isRoot ? 0 : indentation + 2));
                    if(inline && commas > 0) {
                        sb.Append(", ");
                        commas--;
                    }
                }

                if(inline) {
                    sb.Append(IsDictionary ? " }" : " ]");
                }
            }

            return sb.ToString();
        }

    }

}
