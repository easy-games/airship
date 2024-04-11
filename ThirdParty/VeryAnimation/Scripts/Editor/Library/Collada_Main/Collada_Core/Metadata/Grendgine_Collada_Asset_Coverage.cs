using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
#pragma warning disable 0169
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Asset_Coverage
    {
        [XmlElement(ElementName = "geographic_location")]
		Grendgine_Collada_Geographic_Location Geographic_Location;
    }
}

//check done