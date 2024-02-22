
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[LuauAPI]
[HelpURL("https://docs.airship.gg/tags")]
public class TagManager : MonoBehaviour {
    private readonly Dictionary<string, HashSet<GameObject>> tagged = new();

    /**
     * Params: (string) tag, (GameObject) object
     */
    public event Action<object, object> OnTagAdded;
    /**
     * Params: (string) tag, (GameObject) object
     */
    public event Action<object, object> OnTagRemoved;

    private static TagManager instance;
    public static TagManager Instance {
        get {
            if (instance != null) return instance;
            
            var gameObject = new GameObject("TagManager");
            var tagManager = gameObject.AddComponent<TagManager>();
            return tagManager;
        }
    }

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(this);
        }
        instance = this;
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
            OnTagAdded?.Invoke(tag, tagged.gameObject);
        }
    }
    
    internal void UnregisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.GetAllTags()) {
            if (this.TryGetTagSet(tag, out var tagSet)) {
                tagSet.Remove(tagged.gameObject);
                OnTagRemoved?.Invoke(tag, tagged.gameObject);
            }
        }
    }


    
    public bool AddTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>() ?? gameObject.AddComponent<AirshipTags>();
        var tagReplicator = gameObject.GetComponent<AirshipTagReplicator>();

        var tags = GetOrCreateTagSet(tag);
        if (tags.Contains(gameObject)) return false;

        tags.Add(gameObject);
        tagComponent.TagAdded(tag);
        if (tagReplicator != null && RunCore.IsServer()) {
            tagReplicator.TagAdded(tag);
        }
        
        OnTagAdded?.Invoke(tag, gameObject);

        return true;
    }

    public bool HasTag(GameObject gameObject, string tag) {
        return tagged.TryGetValue(tag, out var tags) && tags.Contains(gameObject);
    }

    public bool RemoveTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>();
        if (tagComponent == null) return false;
        if (!tagged.TryGetValue(tag, out var tags)) return false;
        if (!tags.Contains(gameObject)) return false;
        
        tagComponent.TagRemoved(tag);
        var tagReplicator = gameObject.GetComponent<AirshipTagReplicator>();
        if (tagReplicator != null && RunCore.IsServer()) {
            tagReplicator.TagRemoved(tag);
        }
        tags.Remove(gameObject);
        if (tags.Count == 0) {
            tagged.Remove(tag);
        }
        
        OnTagRemoved?.Invoke(tag, gameObject);
        return true;
    }

    public string[] GetAllTagsForGameObject(GameObject gameObject) {
        var tagger = gameObject.GetComponent<AirshipTags>();
        return tagger != null ? tagger.GetAllTags() : new string[] {};
    }

    public GameObject[] GetTagged(string tag) {
        return tagged.TryGetValue(tag, out var tags) ? tags.ToArray() : new GameObject[] { };
    }

    public string[] GetAllTags() {
        return tagged.Keys.ToArray();
    }
}
