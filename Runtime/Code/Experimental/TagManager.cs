
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

    private bool TryGetTagSet(string tag, out HashSet<GameObject> tagSet) {
        return tagged.TryGetValue(tag, out tagSet);
    }

    private HashSet<GameObject> GetOrCreateTagSet(string tag) {
        if (tagged.TryGetValue(tag, out var tags)) return tags;
        
        tags = new HashSet<GameObject>();
        tagged.Add(tag, tags);
        return tags;
    }
    
    internal void RegisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.GetAllTags()) {
            var tagset = GetOrCreateTagSet(tag);
            tagset.Add(tagged.gameObject);
        }
    }
    
    internal void UnregisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.GetAllTags()) {
            if (this.TryGetTagSet(tag, out var tagSet)) {
                tagSet.Remove(tagged.gameObject);
            }
        }
    }


    
    public void AddTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>() ?? gameObject.AddComponent<AirshipTags>();
        var tagReplicator = gameObject.GetComponent<AirshipTagReplicator>();

        var tags = GetOrCreateTagSet(tag);
        Debug.Log($"Try add tag {tag}");

        tags.Add(gameObject);
        tagComponent.TagAdded(tag);
        if (tagReplicator != null) {
            tagReplicator.TagAdded(tag);
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
