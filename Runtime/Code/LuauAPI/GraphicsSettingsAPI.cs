using System;
using UnityEngine.Rendering;

[LuauAPI(LuauContext.Protected)]
public class GraphicsSettingsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(GraphicsSettings);
    }
}