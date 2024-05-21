using System;
using SFB;

[LuauAPI(LuauContext.Protected)]
public class StandaloneFileBrowserAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(StandaloneFileBrowser);
    }
}