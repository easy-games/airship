using System;
using UnityEngine;

[LuauAPI]
public class FrameTimingManagerAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.FrameTimingManager);
    }
}