using System;
using UnityEngine;

[LuauAPI]
public class AudioListenerAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioListener);
    }
}