using System;
using System.Collections.Generic;
using FishNet.Object;
using Unity.VisualScripting;
using UnityEngine;

[AddComponentMenu("Airship/Airship Tags")]
[HelpURL("https://docs.airship.gg/tags")]
[DisallowMultipleComponent]
public class AirshipTags : MonoBehaviour {
    [SerializeField]
    private List<string> tags = new();

    internal void SetTags(string[] tags) {
        this.tags = new List<string>(tags);
    }
    
    internal void TagAdded(string tag) {
        this.tags.Remove(tag);
    }
    
    internal void TagRemoved(string tag) {
        this.tags.Remove(tag);
    }

    public string[] GetAllTags() {
        return this.tags.ToArray();
    }

    public void AddTag(string tag) {
        if (TagManager.Instance.AddTag(this.gameObject, tag))
            this.tags.Add(tag);
    }

    public void RemoveTag(string tag) {
        if (TagManager.Instance.RemoveTag(this.gameObject, tag)) 
            this.tags.Remove(tag);
    }
    
    public bool HasTag(string tag) {
        return this.tags.Contains(tag);
    }

    private void Awake() {
        var networkObject = GetComponent<NetworkObject>() ?? GetComponentInParent<NetworkObject>();
        var replicator = GetComponent<AirshipTagsReplicator>();
        if (networkObject != null && replicator == null) {
            this.AddComponent<AirshipTagsReplicator>();
        }
        
        var tagManager = TagManager.Instance;
        tagManager.RegisterAllTagsForGameObject(gameObject, tags);
    }

    private void OnDestroy() {
        if (!TagManager.IsAwake) return;
        Debug.Log($"Destroy airship tags on {gameObject.name}");
        var tagManager = TagManager.Instance;
        tagManager.UnregisterAllTagsForGameObject(gameObject, tags);
    }
}
