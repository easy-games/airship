using System;
using UnityEngine;

[LuauAPI]
public class AudioClipAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioClip);
    }
}