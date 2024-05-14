using System;

[LuauAPI]
public class GizmosAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Gizmos);
    }
}