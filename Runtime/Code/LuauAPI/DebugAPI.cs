using System;
using UnityEngine;

[LuauAPI]
public class DebugAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Debug);
    }
}