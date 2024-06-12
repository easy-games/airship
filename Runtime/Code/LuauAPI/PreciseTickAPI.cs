using System;
using FishNet.Managing.Timing;

[LuauAPI]
public class PreciseTickAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(PreciseTick);
    }
}