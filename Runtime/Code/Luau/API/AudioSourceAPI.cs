using System;
using UnityEngine;

[LuauAPI]
public class AudioSourceAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioSource);
    }
}