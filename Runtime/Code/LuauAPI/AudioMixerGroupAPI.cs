using System;
using UnityEngine.Audio;

[LuauAPI]
public class AudioMixerGroupAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioMixerGroup);
    }
}