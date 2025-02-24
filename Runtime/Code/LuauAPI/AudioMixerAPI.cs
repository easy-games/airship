using System;
using UnityEngine.Audio;

[LuauAPI]
public class AudioMixerAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioMixer);
    }
}