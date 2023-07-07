using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	[System.Xml.Serialization.XmlRootAttribute(ElementName="technique", Namespace="http://www.collada.org/2005/11/COLLADASchema", IsNullable=true)]
	public partial class Grendgine_Collada_Effect_Technique_COMMON
    {
        [XmlAttribute("sid")]
        public string sID;

        [XmlAttribute("id")]
        public string id;

        [XmlElement(ElementName = "asset")]
        public Grendgine_Collada_Asset Asset;

        [XmlElement(ElementName = "annotate")]
        public Grendgine_Collada_Annotate[] Annotate;

        [XmlElement(ElementName = "blinn")]
		public Grendgine_Collada_Blinn Blinn;
		
		[XmlElement(ElementName = "constant")]
		public Grendgine_Collada_Constant Constant;
		
		[XmlElement(ElementName = "lambert")]
		public Grendgine_Collada_Lambert Lambert;
		
		[XmlElement(ElementName = "phong")]
		public Grendgine_Collada_Phong Phong;

        [XmlElement(ElementName = "extra")]
        public Grendgine_Collada_Extra[] Extra;
    }
}

//check done