using System;
using UnityEngine;

[LuauAPI]
public class StaticBatchingUtilityAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(StaticBatchingUtility);
    }
}