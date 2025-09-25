using System;
using UnityEngine.InputSystem;

[LuauAPI]
public class TouchscreenAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Touchscreen);
    }
}