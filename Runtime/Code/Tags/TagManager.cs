
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GameKit.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (instance == null) {
                var existingInstance = GameObject.FindFirstObjectByType<TagManager>();
                if (existingInstance) {
                    instance = existingInstance;
                    return existingInstance;
                }
            }
            
            var gameObject = new GameObject("TagManager");
            var tagManager = gameObject.AddComponent<TagManager>();
            return tagManager;
        }
    }

    public static bool IsActive {
        get => instance != null && instance.gameObject.scene.isLoaded;
    }

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(this);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(this);
        
        SceneManager.sceneUnloaded += SceneUnloaded;
    }

    private void SceneUnloaded(Scene scene) {
        foreach (var hashSet in this.tagged.Values) {
            foreach (var gameObject in hashSet) {
                if (gameObject.IsDestroyed()) {
                    hashSet.Remove(gameObject);
                }
            }
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


    
    public bool AddTag(GameObject gameObject, string tag) {
        var tagComponent = gameObject.GetComponent<AirshipTags>() ?? gameObject.AddComponent<AirshipTags>();
        var tagReplicator = gameObject.GetComponent<AirshipTagsReplicator>();

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
        var tagReplicator = gameObject.GetComponent<AirshipTagsReplicator>();
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

    private void OnDestroy() {
        Debug.Log($"Destroy TagManager");
        
        // Drop all tagged references
        foreach (var hashSet in tagged.Values) {
            hashSet.Clear();
        }
        tagged.Clear();
        
        Debug.Log("Cleaned up TagManager set");
    }
}
