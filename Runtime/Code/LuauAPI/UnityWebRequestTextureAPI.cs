using System;
using UnityEngine.Networking;

[LuauAPI]
public class UnityWebRequestTextureAPI : BaseLuaAPIClass{
    public override Type GetAPIType() {
        return typeof(UnityWebRequestTexture);
    }
}