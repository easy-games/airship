using System;
using System.Collections.Generic;
using FishNet.Object;
using Unity.VisualScripting;
using UnityEngine;

public class AirshipTags : MonoBehaviour {
    [SerializeField]
    private List<string> tags = new();

    internal void SetTags(string[] tags) {
        this.tags = new List<string>(tags);
    }
    
    internal void TagAdded(string tag) {
        Debug.Log($"Local tag added: {tag}");
        this.tags.Remove(tag);
    }
    
    internal void TagRemoved(string tag) {
        Debug.Log($"Local tag removed: {tag}");
        this.tags.Remove(tag);
    }
    
    public string[] GetAllTags() {
        return this.tags.ToArray();
    }

    public void AddTag(string tag) {
        TagManager.Instance.AddTag(this.gameObject, tag);
        this.tags.Add(tag);
    }

    public void RemoveTag(string tag) {
        TagManager.Instance.RemoveTag(this.gameObject, tag);
        this.tags.Remove(tag);
    }
    
    public bool HasTag(string tag) {
        return this.tags.Contains(tag);
    }

    private void Awake() {
        var networkObject = this.GetComponent<NetworkObject>();
        if (networkObject != null) {
            this.AddComponent<AirshipTagReplicator>();
        }
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
