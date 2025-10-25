using System;
using UnityEngine;

[LuauAPI(ContextOverrideList = new [] {
    // Protected methods
    "OpenURL",
    "RequestUserAuthorization",
    "RequestAdvertisingIdentifierAsync",
    "CaptureScreenshot",
    "Unload",
    
    // Protected file path members
    "persistentDataPath",
    "dataPath",
    "temporaryCachePath",
    "consoleLogPath",
}, ContextOverrideMask = (int) LuauContext.Protected)]
public class ApplicationAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Application);
    }
}