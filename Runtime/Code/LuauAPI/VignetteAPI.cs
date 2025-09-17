using System;
using UnityEngine.Rendering.Universal;

[LuauAPI]
public class VignetteAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Vignette);
    }
}