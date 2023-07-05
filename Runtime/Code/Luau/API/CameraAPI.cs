using System;
using UnityEngine;

[LuauAPI]
public class CameraAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Camera);
    }
}