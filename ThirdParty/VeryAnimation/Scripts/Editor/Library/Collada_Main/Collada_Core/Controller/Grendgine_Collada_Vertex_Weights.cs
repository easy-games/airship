using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Vertex_Weights
	{
		[XmlAttribute("count")]
		public uint Count;

        [XmlElement(ElementName = "input")]
        public Grendgine_Collada_Input_Shared[] Input;

        [XmlElement(ElementName = "vcount")]
		public Grendgine_Collada_Int_Array_String VCount;		

		[XmlElement(ElementName = "v")]
		public Grendgine_Collada_Int_Array_String V;		

	    [XmlElement(ElementName = "extra")]
		public Grendgine_Collada_Extra[] Extra;	
	}
}

//check done