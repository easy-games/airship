using System;
using UnityEngine;

[LuauAPI]
public class CanvasAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Canvas);
    }
}