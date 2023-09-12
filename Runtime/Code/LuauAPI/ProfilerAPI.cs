using System;
using UnityEngine;
using UnityEngine.Profiling;

[LuauAPI]
public class ProfilerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Profiler);
    }
}