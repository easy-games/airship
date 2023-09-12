using System;
using FishNet;

[LuauAPI]
public class InstanceFinderAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(InstanceFinder);  
    }
}