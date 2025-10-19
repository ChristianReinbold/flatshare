using System.Xml;
using System.Xml.Serialization;

namespace de.creinbold.FlatShare
{
    public static class XmlSerializeUtils
    {
        public static XmlSerializerNamespaces EMPTY_NS = new XmlSerializerNamespaces(new XmlQualifiedName[] {
            new XmlQualifiedName(string.Empty, "aaa:abc")
        });
    }
}
