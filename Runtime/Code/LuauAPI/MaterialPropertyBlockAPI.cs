using System;
using UnityEngine;

[LuauAPI]
public class MaterialPropertyBlockAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(MaterialPropertyBlock);
    }
}