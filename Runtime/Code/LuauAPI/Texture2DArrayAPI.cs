using System;
using UnityEngine;

[LuauAPI]
public class Texture2DArrayAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Texture2DArray);
    }
}