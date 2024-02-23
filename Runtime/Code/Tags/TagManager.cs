
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GameKit.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
[HelpURL("https://docs.airship.gg/other/tags")]
[RequireComponent(typeof(TagManagerReplicator))]
public class TagManager : Singleton<TagManager> {
    private readonly Dictionary<string, HashSet<GameObject>> tagged = new();

    /**
     * Params: (string) tag, (GameObject) object
     */
    public event Action<object, object> OnTagAdded;
    /**
     * Params: (string) tag, (GameObject) object
     */
    public event Action<object, object> OnTagRemoved;

    private TagManagerReplicator _managerReplicator;
    public TagManagerReplicator Replicator => _managerReplicator;
    
    private static bool isActive = true;

    private void Awake() {
        _managerReplicator = gameObject.GetComponent<TagManagerReplicator>();
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
    
    internal void RegisterAllTagsForGameObject(GameObject tagged, List<string> tags) {
        Debug.Log($"Register all tags for GameObject [{string.Join(", ", tags)}]", gameObject);
        foreach (var tag in tags) {
            var tagSet = GetOrCreateTagSet(tag);
            tagSet.Add(tagged.gameObject);
            OnTagAdded?.Invoke(tag, tagged.gameObject);
        }
    }
    
    internal void UnregisterAllTagsForGameObject(GameObject tagged, List<string> tags) {
        Debug.Log($"Unregister all tags for GameObject [{string.Join(", ", tags)}]", gameObject);
        foreach (var tag in tags) {
            if (this.TryGetTagSet(tag, out var tagSet)) {
                if (!tagSet.Contains(tagged)) continue;
                tagSet.Remove(tagged.gameObject);
                OnTagRemoved?.Invoke(tag, tagged.gameObject);
                
                // Clear empty sets
                if (tagSet.Count == 0) 
                    this.tagged.Remove(tag);
            }
        }
    }

    internal bool AddTagInternal(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>() ?? gameObject.AddComponent<AirshipTags>();
 
        var tags = GetOrCreateTagSet(tag);
        if (tags.Contains(gameObject)) return false;

        tags.Add(gameObject);
        tagComponent.TagAdded(tag);
        
        OnTagAdded?.Invoke(tag, gameObject);
        return true;
    }
    
    public bool AddTag(GameObject gameObject, string tag) {
        if (!AddTagInternal(gameObject, tag)) return false;
        
        var networkObject = gameObject.GetComponent<NetworkObject>();
        if (networkObject != null) {
            _managerReplicator.TagAddedToNob(networkObject, tag);
        }

        return true;
    }
    
    internal bool RemoveTagInternal(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>();
        if (tagComponent == null) return false;
        if (!tagged.TryGetValue(tag, out var tags)) return false;
        if (!tags.Contains(gameObject)) return false;
        
        tagComponent.TagRemoved(tag);
        tags.Remove(gameObject);
        if (tags.Count == 0) {
            tagged.Remove(tag);
        }
        
        OnTagRemoved?.Invoke(tag, gameObject);
        return true;
    }

    public bool RemoveTag(GameObject gameObject, string tag) {
        if (!this.RemoveTagInternal(gameObject, tag)) return false;
        
        var networkObject = gameObject.GetComponent<NetworkObject>();
        if (networkObject != null) {
            _managerReplicator.TagRemovedFromNob(networkObject, tag);
        }
        
        return true;
    }

    public bool HasTag(GameObject gameObject, string tag) {
        return tagged.TryGetValue(tag, out var tags) && tags.Contains(gameObject);
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

    private void OnDestroy() {
        isActive = false;
        Debug.Log($"Destroy TagManager");
        
        // Drop all tagged references
        foreach (var hashSet in tagged.Values) {
            hashSet.Clear();
        }
        tagged.Clear();
        
        Debug.Log("Cleaned up TagManager set");
    }
}
