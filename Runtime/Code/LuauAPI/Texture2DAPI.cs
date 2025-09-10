using System;
using UnityEngine;

[LuauAPI]
public class Texture2DAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Texture2D);
    }
}