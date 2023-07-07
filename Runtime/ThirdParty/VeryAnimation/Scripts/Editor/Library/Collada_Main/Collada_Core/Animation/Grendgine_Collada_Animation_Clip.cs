using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]
	public partial class Grendgine_Collada_Animation_Clip
	{

		[XmlAttribute("id")]
		public string ID;

        [XmlAttribute("start")]
        public double Start;

        [XmlAttribute("end")]
        public double End;

        [XmlAttribute("name")]
		public string Name;

        [XmlElement(ElementName = "asset")]
        public Grendgine_Collada_Asset Asset;

        [XmlElement(ElementName = "instance_animation")]
        public Grendgine_Collada_Instance_Animation[] Instance_Animation;	

        //1.5
        //[XmlElement(ElementName = "instance_formula")]
        //public Grendgine_Collada_Instance_Formula[] Instance_Formula;	

        [XmlElement(ElementName = "extra")]
		public Grendgine_Collada_Extra[] Extra;	
	}
}

//check done