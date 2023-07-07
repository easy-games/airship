using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Input_Unshared
	{
		[XmlAttribute("semantic")]
        public Grendgine_Collada_Input_Semantic Semantic;	

		[XmlAttribute("source")]
		public string source;

	}
}

//check done