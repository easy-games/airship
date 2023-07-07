using System;
namespace VeryAnimation.grendgine_collada
{
	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.collada.org/2005/11/COLLADASchema" )]
	public enum Grendgine_Collada_FX_Surface_Type
	{
        UNTYPED,

        [System.Xml.Serialization.XmlEnumAttribute("1D")]
        _1D,

        [System.Xml.Serialization.XmlEnumAttribute("2D")]
        _2D,

        [System.Xml.Serialization.XmlEnumAttribute("3D")]
        _3D,

        CUBE,

        DEPTH,

        RECT,
    }
}

//new create