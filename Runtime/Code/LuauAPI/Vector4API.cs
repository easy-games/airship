using System;

[LuauAPI]
public class Vector4API : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Vector4);
    }
}