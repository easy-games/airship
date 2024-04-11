using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	[System.Xml.Serialization.XmlRootAttribute(ElementName="surface", Namespace="http://www.collada.org/2005/11/COLLADASchema", IsNullable=true)]
	public partial class Grendgine_Collada_Surface_1_4_1
    {
        [XmlAttribute("type")]
        public Grendgine_Collada_FX_Surface_Type Type;

        [XmlElement(ElementName = "init_from")]
        public string Init_From;
    }
}

//new create