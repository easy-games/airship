using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Mesh
	{
		
		
	    [XmlElement(ElementName = "source")]
		public Grendgine_Collada_Source[] Source;
        
        [XmlElement(ElementName = "vertices")]
        public Grendgine_Collada_Vertices Vertices;

        [XmlElement(ElementName = "lines")]
		public Grendgine_Collada_Lines[] Lines;		
	    
		[XmlElement(ElementName = "linestrips")]
		public Grendgine_Collada_Linestrips[] Linestrips;		

	    [XmlElement(ElementName = "polygons")]
		public Grendgine_Collada_Polygons[] Polygons;		

	    [XmlElement(ElementName = "polylist")]
		public Grendgine_Collada_Polylist[] Polylist;		
		
	    [XmlElement(ElementName = "triangles")]
		public Grendgine_Collada_Triangles[] Triangles;		
		
	    [XmlElement(ElementName = "trifans")]
		public Grendgine_Collada_Trifans[] Trifans;		
		
	    [XmlElement(ElementName = "tristrips")]
		public Grendgine_Collada_Tristrips[] Tristrips;
		
	    [XmlElement(ElementName = "extra")]
		public Grendgine_Collada_Extra[] Extra;		
	}
}

//check done