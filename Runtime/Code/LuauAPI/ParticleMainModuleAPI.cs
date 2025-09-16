using System;
using UnityEngine;

[LuauAPI]
public class ParticleMainModuleAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(ParticleSystem.MainModule);
    }
}