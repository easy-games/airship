using System.Collections.Generic;
using UnityEngine;

public class CloudBridge : Singleton<CloudBridge> {
    public Dictionary<string, Texture2D> textures = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public void OnLoad() {
        textures.Clear();
    }
}