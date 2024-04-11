using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Accessor
	{
		[XmlAttribute("count")]
		public uint Count;

		[XmlAttribute("offset")]
        [System.ComponentModel.DefaultValueAttribute(typeof(uint), "0")]
        public uint Offset;		
		
		[XmlAttribute("source")]
		public string Source;		
		
		[XmlAttribute("stride")]
        [System.ComponentModel.DefaultValueAttribute(typeof(uint), "1")]
        public uint Stride = 1;		
		
	    [XmlElement(ElementName = "param")]
		public Grendgine_Collada_Param[] Param;				
	}
}

