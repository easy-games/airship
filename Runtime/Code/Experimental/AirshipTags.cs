using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class AirshipTags : NetworkBehaviour {
    [SerializeField]
    private List<string> tags = new();
    
    public List<string> Tags => tags;

    [ObserversRpc(ExcludeServer = true)]
    internal void TagAdded(string tag) {
        this.AddTag(tag);
    }

    [ObserversRpc(ExcludeServer = true)]
    internal void TagRemoved(string tag) {
        this.RemoveTag(tag);
    }
   
    public void AddTag(string tag) {
        TagManager.Instance.AddTag(this.gameObject, tag);
        tags.Add(tag);
    }

    public bool HasTag(string tag) {
        return tags.Contains(tag);
    }

    public void RemoveTag(string tag) {
        TagManager.Instance.RemoveTag(this.gameObject, tag);
        tags.Remove(tag);
    }

    private void Start() {
        var tagManager = TagManager.Instance;
        tagManager.RegisterAllTagsForGameObject(this);
    }

    private void OnDestroy() {
        var tagManager = TagManager.Instance;
        tagManager.UnregisterAllTagsForGameObject(this);
    }
}
