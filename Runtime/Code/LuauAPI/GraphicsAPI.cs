using System;
using UnityEngine;

[LuauAPI]
public class GraphicsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Graphics);
    }
}