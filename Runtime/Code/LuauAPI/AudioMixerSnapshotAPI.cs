using System;
using UnityEngine.Audio;

[LuauAPI]
public class AudioMixerSnapshotAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(AudioMixerSnapshot);
    }
}