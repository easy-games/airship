
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[LuauAPI]
public class TagManager : NetworkBehaviour {
    private readonly Dictionary<string, HashSet<GameObject>> tagged = new();

    private static TagManager instance;
    public static TagManager Instance {
        get {
            if (instance != null) return instance;
            
            var gameObject = new GameObject("TagManager");
            var tagManager = gameObject.AddComponent<TagManager>();
            instance = tagManager;

            return instance;
        }
    }

    internal void RegisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.GetAllTags()) {
            this.AddTag(tagged.gameObject, tag);
        }
    }
    
    internal void UnregisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.GetAllTags()) {
            this.RemoveTag(tagged.gameObject, tag);
        }
    }

    public void AddTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>() ?? gameObject.AddComponent<AirshipTags>();
        var tagReplicator = gameObject.GetComponent<AirshipTagReplicator>();
        
        if (tagged.TryGetValue(tag, out var tags)) {
            Debug.Log($"Try add tag {tag}");
            if (tags.Contains(gameObject)) return;
            tags.Add(gameObject);

            tagComponent.TagAdded(tag);
            if (tagReplicator != null) {
                tagReplicator.TagAdded(tag);
            }
        }
        else {
            Debug.Log($"Try add tag {tag}");
            tags = new HashSet<GameObject>();
            tagged.Add(tag, tags);
            tags.Add(gameObject);

            tagComponent.TagAdded(tag);
            if (tagReplicator != null) {
                tagReplicator.TagAdded(tag);
            }
        }
    }

    public bool HasTag(GameObject gameObject, string tag) {
        return tagged.TryGetValue(tag, out var tags) && tags.Contains(gameObject);
    }

    public void RemoveTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>();
        if (tagComponent == null) return;
        if (!tagged.TryGetValue(tag, out var tags)) return;
        
        tagComponent.TagRemoved(tag);
        var tagReplicator = gameObject.GetComponent<AirshipTagReplicator>();
        if (tagReplicator != null) {
            tagReplicator.TagRemoved(tag);
        }
        tags.Remove(gameObject);
    }

    public string[] GetAllTagsForGameObject(GameObject gameObject) {
        var tagger = gameObject.GetComponent<AirshipTags>();
        return tagger != null ? tagger.GetAllTags() : new string[] {};
    }

    public GameObject[] GetTagged(string tag) {
        return tagged.TryGetValue(tag, out var tags) ? tags.ToArray() : new GameObject[] { };
    }
}
