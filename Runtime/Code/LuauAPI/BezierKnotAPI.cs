using System;
using UnityEngine.Splines;

[LuauAPI]
public class BezierKnotAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(BezierKnot);
    }
}