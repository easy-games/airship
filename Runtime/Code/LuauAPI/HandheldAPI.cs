using System;
using UnityEngine;

[LuauAPI]
public class HandheldAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Handheld);
    }
}