using System;
using Unity.Jobs;

[LuauAPI]
public class JobHandleAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(JobHandle);
    }
}