using System;

[LuauAPI]
public class Physics2DAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Physics2D);
    }
}