
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

[LuauAPI]
public class TagManager : NetworkBehaviour {
    private Dictionary<string, HashSet<GameObject>> tagged = new();

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
        foreach (var tag in tagged.Tags) {
            this.AddTag(tagged.gameObject, tag);
        }
    }

    internal void UnregisterAllTagsForGameObject(AirshipTags tagged) {
        foreach (var tag in tagged.Tags) {
            this.RemoveTag(tagged.gameObject, tag);
        }
    }

    public void AddTag(GameObject gameObject, string tag) {
        if (tagged.TryGetValue(tag, out var tags)) {
            Debug.Log($"Add tag {tag} to {gameObject.name}");
            tags.Add(gameObject);
        }
        else {
            tags = new HashSet<GameObject>();
            tagged.Add(tag, tags);
            Debug.Log($"Add tag {tag} to {gameObject.name}");
            tags.Add(gameObject);
        }
    }

    public bool HasTag(GameObject gameObject, string tag) {
        return tagged.TryGetValue(tag, out var tags) && tags.Contains(gameObject);
    }

    public void RemoveTag(GameObject gameObject, string tag) {
        if (tagged.TryGetValue(tag, out var tags)) {
            Debug.Log($"Remove tag {tag} to {gameObject.name}");
            tags.Remove(gameObject);
        }
    }

    public GameObject[] GetTagged(string tag) {
        return tagged.TryGetValue(tag, out var tags) ? tags.ToArray() : new GameObject[] { };
    }
}
