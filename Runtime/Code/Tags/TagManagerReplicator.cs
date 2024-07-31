using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TagManager))]
public class TagManagerReplicator : NetworkBehaviour {
    // [ObserversRpc(ExcludeServer = true, RunLocally = false)]
    // internal void TagAddedToNob(NetworkObject nob, string tag) {
    //     TagManager.Instance.AddTagInternal(nob.gameObject, tag);
    // }
    //
    // [ObserversRpc(ExcludeServer = true, RunLocally = false)]
    // internal void TagRemovedFromNob(NetworkObject nob, string tag) {
    //     TagManager.Instance.RemoveTagInternal(nob.gameObject, tag);
    // }
    //
    // [ServerRpc(RequireOwnership = false)]
    // internal void RequestServerTagsForNob(NetworkObject nob, NetworkConnection request = null)
    // {
    //     var tags = TagManager.Instance.GetAllTagsForGameObject(nob.gameObject);
    //     if (tags.Length > 0) {
    //         UpdateTagsForNob(request, nob, tags);
    //     }
    // }
    //
    // [TargetRpc]
    // internal void UpdateTagsForNob(NetworkConnection target, NetworkObject nob, string[] tags) {
    //     var gameObject = nob.gameObject;
    //     foreach (var tag in tags) {
    //         TagManager.Instance.AddTagInternal(gameObject, tag);
    //     }
    // }
}
