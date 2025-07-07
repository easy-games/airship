using System;
using UnityEngine.Splines;

[LuauAPI]
public class BezierCurveAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(BezierCurve);
    }
}