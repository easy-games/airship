using System.Collections.Generic;
using Mirror;
using UnityEngine;

[AddComponentMenu("Airship/Airship Tags")]
[HelpURL("https://docs.airship.gg/other/tags")]
[DisallowMultipleComponent]
public class AirshipTags : MonoBehaviour {
    [SerializeField]
    private List<string> tags = new();

    internal void SetTags(string[] tags) {
        this.tags = new List<string>(tags);
    }
    
    internal void TagAdded(string tag) {
        this.tags.Add(tag);
    }
    
    internal void TagRemoved(string tag) {
        this.tags.Remove(tag);
    }

    public string[] GetAllTags() {
        return this.tags.ToArray();
    }

    public void AddTag(string tag) {
        TagManager.Instance.AddTag(this.gameObject, tag);
    }

    public void RemoveTag(string tag) {
        TagManager.Instance.RemoveTag(this.gameObject, tag);
    }
    
    public bool HasTag(string tag) {
        return this.tags.Contains(tag);
    }

    private void Awake() {
        var tagManager = TagManager.Instance;
        tagManager.RegisterAllTagsForGameObject(gameObject, tags);

        var networkObject = this.gameObject.GetComponent<NetworkIdentity>() ??
                            this.gameObject.GetComponentInParent<NetworkIdentity>();

        if (networkObject != null && !RunCore.IsServer()) {
            // todo: mirror migration
            // var replicator = TagManager.Instance.Replicator;
            // replicator.RequestServerTagsForNob(networkObject);
        }
        
    }

    private void OnDestroy() {
        if (!TagManager.IsAwake) return;
        var tagManager = TagManager.Instance;
        tagManager.UnregisterAllTagsForGameObject(gameObject, tags);
    }
}
