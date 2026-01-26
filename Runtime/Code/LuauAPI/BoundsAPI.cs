using System;
using UnityEngine;

[LuauAPI]
public class BoundsAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(Bounds);
    }
}