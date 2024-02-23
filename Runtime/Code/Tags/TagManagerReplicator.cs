using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TagManager))]
public class TagManagerReplicator : NetworkBehaviour {
    [ObserversRpc(ExcludeServer = true, RunLocally = false)]
    internal void TagAddedToNob(NetworkObject nob, string tag) {
#if AIRSHIP_INTERNAL
        Debug.Log($"Added tag to {nob.gameObject.name}: '{tag}'");
#endif
        TagManager.Instance.AddTagInternal(nob.gameObject, tag);
    }

    [ObserversRpc(ExcludeServer = true, RunLocally = false)]
    internal void TagRemovedFromNob(NetworkObject nob, string tag) {
#if AIRSHIP_INTERNAL
        Debug.Log($"Removed tag from {nob.gameObject.name}: '{tag}'");
#endif
        TagManager.Instance.RemoveTagInternal(nob.gameObject, tag);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void RequestServerTagsForNob(NetworkObject nob, NetworkConnection request = null) 
    {
#if AIRSHIP_INTERNAL
        Debug.Log($"Recieved request for tags from client {nob.gameObject.name}", nob);
#endif
        var tags = TagManager.Instance.GetAllTagsForGameObject(nob.gameObject);
        if (tags.Length > 0) {
            UpdateTagsForNob(request, nob, tags);
        }
    }

    [TargetRpc]
    internal void UpdateTagsForNob(NetworkConnection target, NetworkObject nob, string[] tags) {
#if AIRSHIP_INTERNAL
        Debug.Log($"Update tags for {nob.ObjectId} ({nob.gameObject.name}): {string.Join(", ", tags)}", nob);
#endif
        
        var gameObject = nob.gameObject;
        foreach (var tag in tags) {
            TagManager.Instance.AddTagInternal(gameObject, tag);
        }
    }

    public override void OnStartClient() {
        print("start tag manager on client");
    }
}
