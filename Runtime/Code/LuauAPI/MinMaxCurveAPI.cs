using System;
using UnityEngine;

[LuauAPI]
public class MinMaxCurveAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(ParticleSystem.MinMaxCurve);
    }
}