using System;
using UnityEngine.Splines;

[LuauAPI]
public class SplineAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Spline);
    }
}