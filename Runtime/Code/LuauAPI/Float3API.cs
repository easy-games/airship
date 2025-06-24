using System;
using Unity.Mathematics;

[LuauAPI]
public class Float3API : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(float3);
    }
}