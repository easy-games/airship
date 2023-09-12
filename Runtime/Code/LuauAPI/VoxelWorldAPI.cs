using System;

[LuauAPI]
public class VoxelWorldAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(VoxelWorld);
    }
}