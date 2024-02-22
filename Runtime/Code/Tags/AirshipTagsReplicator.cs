using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[AddComponentMenu("Airship/Networking/Airship Tags Replicator")]
[RequireComponent(typeof(AirshipTags))]
[DisallowMultipleComponent]
[HelpURL("https://docs.airship.gg/other/tags")]
public class AirshipTagsReplicator : NetworkBehaviour {
    private AirshipTags tags;
    internal NetworkObject networkObject;
    
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
        this.networkObject = GetComponent<NetworkObject>() ?? GetComponentInParent<NetworkObject>();
#if !AIRSHIP_INTERNAL
        this.hideFlags = HideFlags.HideInInspector;
#endif
    }
}
