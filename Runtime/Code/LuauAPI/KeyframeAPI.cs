using System;
using UnityEngine;

[LuauAPI]
public class KeyframeAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Keyframe);
    }
}