using System;
using UnityEngine;

[LuauAPI]
public class AnimationCurveAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(AnimationCurve);
    }
}