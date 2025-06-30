using System;
using UnityEngine.Splines;

[LuauAPI]
public class SplineContainerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(SplineContainer);
    }
}