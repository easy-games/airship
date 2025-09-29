using System;
using System.Collections.Generic;
using UnityEngine;

[LuauAPI(LuauContext.Protected, ContextOverrideList = new []{ "GetQualityLevel", "names" })]
public class QualitySettingsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(QualitySettings);
    }
}