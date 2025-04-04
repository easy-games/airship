using System;
using UnityEngine;

[LuauAPI(LuauContext.Protected)]
public class QualitySettingsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(QualitySettings);
    }
}