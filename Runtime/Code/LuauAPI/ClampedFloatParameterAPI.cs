using System;
using UnityEngine.Rendering;

[LuauAPI]
public class ClampedFloatParameterAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(ClampedFloatParameter);
    }
}