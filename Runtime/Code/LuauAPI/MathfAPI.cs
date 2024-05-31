using System;
using UnityEngine;

[LuauAPI]
public class MathfAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Mathf);
    }
}