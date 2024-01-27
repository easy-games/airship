using System;

[LuauAPI]
public class MeshAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Mesh);
    }
}