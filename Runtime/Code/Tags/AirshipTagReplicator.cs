using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(AirshipTags))]
[HelpURL("https://docs.airship.gg/tags")]
public class AirshipTagReplicator : NetworkBehaviour {
    internal AirshipTags tags;
    
    [ObserversRpc]
    internal void TagAdded(string tag) {
        this.tags.AddTag(tag);
    }

    [ObserversRpc]
    internal void TagRemoved(string tag) {
        this.tags.RemoveTag(tag);
    }

    [ObserversRpc(BufferLast = true)]
    internal void SetTags(string[] tags) {
        this.tags.SetTags(tags);
    }
    
    private void Awake() {
        this.tags = GetComponent<AirshipTags>();
#if !AIRSHIP_INTERNAL
        this.hideFlags = HideFlags.HideInInspector;
#endif
    }
}
