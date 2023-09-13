using System;
using UnityEngine;

[LuauAPI]
public class RectTransformAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(RectTransform);
    }
}