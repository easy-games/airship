using System;
using UnityEditor;

[LuauAPI(LuauContext.Protected)]
public class SessionStateAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(SessionState);
    }
}