using System;

[LuauAPI]
public class RenderTextureAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.RenderTexture);
    }
}