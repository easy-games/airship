using System;

[LuauAPI(LuauContext.Protected)]
public class QualitySettingsAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(QualitySettingsAPI);
    }
}