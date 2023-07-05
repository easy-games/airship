using System;
using UnityEngine;

public class ColorAPI : BaseLuaAPIClass
{
	public override Type GetAPIType()
	{
		return typeof(Color);
	}
}