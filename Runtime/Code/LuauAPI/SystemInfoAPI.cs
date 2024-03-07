using System;
using UnityEngine;

[LuauAPI]
public class SystemInfoAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(SystemInfo);
    }
}