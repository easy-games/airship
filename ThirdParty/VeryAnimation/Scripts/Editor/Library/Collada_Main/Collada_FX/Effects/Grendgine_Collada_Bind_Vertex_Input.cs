using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	[System.Xml.Serialization.XmlRootAttribute(ElementName="bind_vertex_input", Namespace="http://www.collada.org/2005/11/COLLADASchema", IsNullable=true)]
	public partial class Grendgine_Collada_Bind_Vertex_Input
	{
		[XmlAttribute("input_semantic")]
		public string Input_Semantic;

		[XmlAttribute("input_set")]
		public int Input_Set;

        [XmlAttribute("semantic")]
        public string Semantic;
    }
}

//check done