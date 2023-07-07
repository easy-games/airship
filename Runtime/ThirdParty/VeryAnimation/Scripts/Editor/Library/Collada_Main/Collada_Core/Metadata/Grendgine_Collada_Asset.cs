using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Grendgine_Collada_Asset
    {
        [XmlElement(ElementName = "contributor")]
        public Grendgine_Collada_Asset_Contributor[] Contributor;

        [XmlElement(ElementName = "coverage")]
        public Grendgine_Collada_Asset_Coverage Coverage;

        [XmlElement(ElementName = "created")]
        public System.DateTime Created;

        [XmlElement(ElementName = "keywords")]
        public string Keywords;

        [XmlElement(ElementName = "modified")]
        public System.DateTime Modified;

        [XmlElement(ElementName = "revision")]
        public string Revision;

        [XmlElement(ElementName = "subject")]
        public string Subject;

        [XmlElement(ElementName = "title")]
        public string Title;

        [XmlElement(ElementName = "unit")]
        public Grendgine_Collada_Asset_Unit Unit;

        [XmlElement(ElementName = "up_axis")]
        [System.ComponentModel.DefaultValueAttribute("Y_UP")]
        public string Up_Axis = "Y_UP";

        [XmlElement(ElementName = "extra")]
        public Grendgine_Collada_Extra[] Extra;
    }
}

//check done