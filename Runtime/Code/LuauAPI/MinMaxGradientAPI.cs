using System;
using UnityEngine;

[LuauAPI]
public class MinMaxGradientAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(ParticleSystem.MinMaxGradient);
    }
}