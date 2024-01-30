using System;
using UnityEngine;

[LuauAPI]
public class ShaderAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.Shader);
    }
}