using System;
using UnityEngine;

[LuauAPI]
public class RenderSettingsAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(RenderSettings);
    }
}