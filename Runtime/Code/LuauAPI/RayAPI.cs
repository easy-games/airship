using System;
using UnityEngine;

// [LuauAPI]
public class RayAPI : BaseLuaAPIClass
{
	public override Type GetAPIType() {
		return typeof(Ray);
	}
}