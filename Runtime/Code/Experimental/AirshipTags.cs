using System.Collections.Generic;
using UnityEngine;

public class AirshipTags : MonoBehaviour {
    [SerializeField]
    private List<string> tags = new();
    
    public List<string> Tags => tags;

    public void AddTag(string tag) {
        TagManager.Instance.AddTag(this.gameObject, tag);
        tags.Add(tag);
    }

    public bool HasTag(string tag) {
        return tags.Contains(tag);
    }

    public bool RemoveTag(string tag) {
        TagManager.Instance.RemoveTag(this.gameObject, tag);
        return tags.Remove(tag);
    }

    public string[] GetAllTags() {
        return this.tags.ToArray();
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
