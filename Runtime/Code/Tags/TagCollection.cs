using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[CreateAssetMenu(fileName = "Tags", menuName = "Airship/Tags", order = 100)]
public class TagCollection : ScriptableObject {
    public string collectionName;
    public List<string> tags = new();

    public static List<T> FindAssetsByType<T>() where T : UnityEngine.Object {
#if UNITY_EDITOR
        List<T> assets = new List<T>();

        string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));

        for (int i = 0; i < guids.Length; i++) {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            
            if (asset != null) {
                assets.Add(asset);
            }
        }

        return assets;
#else
        return new();
#endif
    }
    
    public static TagCollection[] GetTagLists() {
        return FindAssetsByType<TagCollection>().ToArray();
    }

    public static TagCollection GetGameTags() {
#if UNITY_EDITOR
        var gameConfig = AssetDatabase.LoadAssetAtPath<TagCollection>("Assets/Tags.asset");
        if (gameConfig == null) {
            gameConfig = CreateInstance<TagCollection>();
            AssetDatabase.CreateAsset(gameConfig, "Assets/Tags.asset");
        }
        
        return gameConfig;
#else
        return null;
#endif
    }
}
