using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_New_Param
	{
		[XmlAttribute("sid")]
		public string sID;

        [XmlElement("annotate")]
        public Grendgine_Collada_Annotate[] Annotate;

        [XmlElement(ElementName = "semantic")]
		public string Semantic;				
		
		[XmlElement(ElementName = "modifier")]
		public string Modifier;

        [XmlElement(ElementName = "sampler2D")]
        public Grendgine_Collada_Sampler2D Sampler2D;

        [XmlElement(ElementName = "surface")]
        public Grendgine_Collada_Surface_1_4_1 Surface;

        /// <summary>
        /// The element is the type and the element text is the value or space delimited list of values
        /// </summary>
        [XmlAnyElement]
		public XmlElement[] Data;	
	}
}

//check done