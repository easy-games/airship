using System;

[LuauAPI]
public class MaterialAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Material);
    }
}