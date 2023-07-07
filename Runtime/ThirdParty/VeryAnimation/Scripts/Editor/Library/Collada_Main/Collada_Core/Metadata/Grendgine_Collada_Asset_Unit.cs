using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Asset_Unit
	{
		[XmlAttribute("meter")]
	    //[System.ComponentModel.DefaultValueAttribute(typeof(double), "1.0")]  //force write
		public double Meter = 1;

		[XmlAttribute("name")]
        //[System.ComponentModel.DefaultValueAttribute("meter")] //force write
        public string Name = "meter";
	}
}

//check done