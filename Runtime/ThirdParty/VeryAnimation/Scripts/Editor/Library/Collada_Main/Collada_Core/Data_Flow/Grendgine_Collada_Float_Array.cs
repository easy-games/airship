using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Float_Array : Grendgine_Collada_Float_Array_String
	{
		[XmlAttribute("id")]
		public string ID;
		
		[XmlAttribute("name")]
		public string Name;			
		
		[XmlAttribute("count")]
		public int Count;		
		
		[XmlAttribute("digits")]
	    [System.ComponentModel.DefaultValueAttribute(typeof(int), "6")]
		public int Digits = 6;		

		[XmlAttribute("magnitude")]
	    [System.ComponentModel.DefaultValueAttribute(typeof(int), "38")]
		public int Magnitude = 38;		
		
	}
}

