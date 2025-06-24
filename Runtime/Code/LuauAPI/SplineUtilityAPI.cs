using System;
using UnityEngine.Splines;

[LuauAPI]
public class SplineUtilityAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(SplineUtility);
    }
}