using System;
using UnityEngine.Networking;

[LuauAPI]
public class DownloadHandlerTextureAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(DownloadHandlerTexture);
    }
}