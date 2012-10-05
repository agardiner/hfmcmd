using System;
using System.Xml;
using System.IO;


namespace YAML
{

    /// <summary>
    /// Adds support for converting an XML document to a YAML equivalent
    /// </summary>
    public static class XML
    {

        /// <summary>
        /// Converts an XML string to the equivalent YAML representation.
        /// </summary>
        public static Node ConvertXML(string xml)
        {
            XmlDocument doc = new XmlDocument();
            Node yaml = new Node();
            doc.Load(new StringReader(xml));
            foreach(XmlNode node in doc) {
                if(node.NodeType == XmlNodeType.Element) {
                    yaml.AddXML(node);
                }
            }

            return yaml;
        }



        /// <summary>
        /// Adds an XML node (and its children) to this YAML node.
        /// </summary>
        public static void AddXML(this Node yaml, XmlNode xml)
        {
            if(xml.HasChildNodes && xml.ChildNodes[0].NodeType != XmlNodeType.Text) {
                var child = yaml.Add(new Node(xml.LocalName, null));
                foreach (XmlNode xmlChild in xml.ChildNodes) {
                    child.AddXML(xmlChild);
                }
            }
            else {
                yaml.Add(new Node(xml.LocalName, xml.InnerText));
            }
        }

    }

}
