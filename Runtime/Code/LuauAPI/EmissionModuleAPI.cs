using System;
using UnityEngine;

[LuauAPI]
public class EmissionModuleAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(ParticleSystem.EmissionModule);
    }
}