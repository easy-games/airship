#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

internal class AssetData {
    /** The asset's _full_ path. */
    public readonly string Path;
    /** Whether or not the asset is a scene object. */
    public readonly bool IsSceneObject;
    /** The name of the org asset belongs to, if applicable. Empty string means asset belongs to game. */
    public readonly string OrgName = String.Empty;
    /** The name of the package asset belongs to, if applicable. Empty string means asset belongs to game. */
    public readonly string PackageName = String.Empty;

    public AssetData(string path) {
        this.Path = path;
        // If we pass in an empty path, this asset is a scene object.
        if (path == String.Empty) {
            IsSceneObject = true;
        }
        else {
            // Otherwise, asset is either a game or package resource.
            IsSceneObject = false;
            // If there isn't an "@" in path, we already know it isn't a package asset.
            if (!path.Contains("@")) return;
            var pathParts = path.Split("/");
            // If the path is comprised of less than 4 parts, it is shallow enough
            // that it couldn't possibly be a package asset.
            if (pathParts.Length < 4) return; 
            var maybeOrg = pathParts[2];
            var maybePackage = pathParts[3];
            if (maybeOrg.StartsWith("@")) {
                OrgName = maybeOrg;
                PackageName = maybePackage;
            } 
        }
    }

    public bool IsInternalAsset() {
        return Path.Contains("gg.easy.airship");
    }

    public bool IsGameAsset() {
        return OrgName == String.Empty || PackageName == String.Empty;
    }

    public bool IsLocalPackageAsset() {
        // It's important to know if a package asset belongs to one of _our_ local packages,
        // or an external package. We don't need to manage external package assets, external
        // packages will handle their own assets.
        if (OrgName == String.Empty || PackageName == String.Empty) return false;
        var gameConfig = GameConfig.Load();
        var packages = gameConfig.packages;
        foreach (var package in packages) {
            var idParts = package.id.Split("/");
            var pOrg = idParts[0];
            var pName = idParts[1];
            if (OrgName == pOrg && PackageName == pName && package.localSource) {
                return true;
            }
        }
        return false;
    }
    
}


[InitializeOnLoad]
public class NetworkPrefabManager {

    // `HashSet` of instance ids of instances that are already inside a collection.
    private static readonly HashSet<int> SessionCollectionCache = new HashSet<int>();

    static NetworkPrefabManager() {
        // We _must_ preload our `NetworkPrefabCollection`s and `NetworkObject`s immediately,
        // otherwise `FindObjectsOfTypeAll` won't pick up all instances.
        Resources.LoadAll("NetworkPrefabCollection", typeof(NetworkPrefabCollection));
        Resources.LoadAll("NetworkObject", typeof(NetworkObject));
        // These listeners are _not_ strictly required. We technically only need to be calling
        // `WriteAllCollections` on play when the project is dirty, and **always** on publish. However,
        // since the cost is negligible, we can do this in real time.
        // EditorApplication.projectChanged -= UpdateNetworkCollections;
        // EditorApplication.projectChanged += UpdateNetworkCollections;
        // ObjectChangeEvents.changesPublished -= OnChangesPublished;
        // ObjectChangeEvents.changesPublished += OnChangesPublished;
        
        // This one is required in some capacity, read above comment.
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;


    }

    private static void UpdateNetworkCollections() {
        EditorCoroutines.Execute(WriteAllCollectionsAsync());
    }

    private static void OnChangesPublished(ref ObjectChangeEventStream stream) {
        for (var i = 0; i < stream.length; i++) {
            var eventType = stream.GetEventType(i);
            switch (eventType) {
                case ObjectChangeKind.UpdatePrefabInstances:
                    EditorCoroutines.Execute(WriteAllCollectionsAsync());
                    break;
            }
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.ExitingEditMode) {
            // WriteAllCollections();
        }
    }

    private static AssetData GetAssetDataFromPath(string path) {
        return new AssetData(path);
    }
    
    private static List<NetworkPrefabCollection> GetCollections() {
        // This ensures that all collections are in the search space, including newly
        // created collections.
        Resources.LoadAll("NetworkPrefabCollection", typeof(NetworkPrefabCollection));
        var allCollections = Resources.FindObjectsOfTypeAll<NetworkPrefabCollection>();
        var validCollections = new List<NetworkPrefabCollection>();
        foreach (var collection in allCollections) {
            var assetPath = AssetDatabase.GetAssetPath(collection);
            var assetData = GetAssetDataFromPath(assetPath);
            if (assetData.IsGameAsset() || assetData.IsLocalPackageAsset()) {
                validCollections.Add(collection);
            }
        }
        return validCollections;
    }

    [CanBeNull]
    private static NetworkPrefabCollection GetGameCollection() {
        var collections = GetCollections();
        foreach (var collection in collections) {
            var assetPath = AssetDatabase.GetAssetPath(collection);
            var assetData = GetAssetDataFromPath(assetPath);
            if (assetData.IsGameAsset()) {
                return collection;
            }
        }
        return null;
    }
    
    [CanBeNull]
    private static NetworkPrefabCollection GetPackageCollection(string orgName, string packageName) {
        var collections = GetCollections();
        foreach (var collection in collections) {
            var assetPath = AssetDatabase.GetAssetPath(collection);
            var assetData = GetAssetDataFromPath(assetPath);
            if (assetData.OrgName == orgName && assetData.PackageName == packageName && assetData.IsLocalPackageAsset()) {
                return collection;
            }
        }
        return null;
    }

    private static FishNet.Object.NetworkObject[] GetNetworkObjects() {
        return Resources.FindObjectsOfTypeAll<NetworkObject>();
    }

    private static void ClearAllCollections() {
        var collections = GetCollections();
        foreach (var collection in collections) {
            collection.networkPrefabs.Clear();
        }
    }

    [MenuItem("Airship/Misc/Generate Network Prefab Collections")]
    public static void WriteAllCollections() {
        SessionCollectionCache.Clear();
        ClearAllCollections();
        var nobs = GetNetworkObjects();
        // Debug.Log("nobs returned: " + nobs.Length);
        // foreach (var nob in nobs) {
        //     Debug.Log("  - " + nob.gameObject.name);
        // }
        foreach (var nob in nobs) {
            var assetPath = AssetDatabase.GetAssetPath(nob);
            var assetData = GetAssetDataFromPath(assetPath);
            WriteToCollection(nob.gameObject, assetData);
        }
    }
    
    public static IEnumerator WriteAllCollectionsAsync() {
        // We must delay for a frame here to make sure that the AssetDatabase has been
        // properly updated as a result of the action that is triggering this write.
        yield return new WaitForSeconds(0);
        WriteAllCollections();
    }
    
    private static void WriteToCollection(GameObject prefab, AssetData data) {
        var isNested = prefab.transform.parent != null;
        // Nested NOB, no need to process this, the root NOB will live in the collection.
        if (isNested) return;
        var instanceId = prefab.GetInstanceID();
        var alreadyInCollection = SessionCollectionCache.Contains(instanceId);
        // If this NOB already belongs to a collection, we can safely skip here.
        if (alreadyInCollection) return;
        // Scene objects cannot exist inside of network prefab collections. (Type mismatch)
        if (data.IsSceneObject) return;
        // For now, we're skipping any asset that is "internal". An asset
        // is internal if it lives inside of the Airship project. For example,
        // the Player.
        if(data.IsInternalAsset()) return;
        if (data.IsLocalPackageAsset()) {
            // This belongs to a _local_ package, this asset should be inside of
            // the network prefab collection that corresponds to the package.
            var packageCollection = GetPackageCollection(data.OrgName, data.PackageName);
            if (packageCollection) {
                packageCollection.networkPrefabs.Add(prefab.gameObject);
                SessionCollectionCache.Add(prefab.GetInstanceID());
            }
            else {
                var newPackageCollection = CreateCollectionByPath(data.Path);
                if (newPackageCollection == null) return;
                newPackageCollection.networkPrefabs.Add(prefab.gameObject);
            }
        }
        if (data.IsGameAsset()) {
            // This doesn't belong to a package, this asset should be inside of
            // the game's network prefab collection.
            var gameCollection = GetGameCollection();
            if (gameCollection) {
                gameCollection.networkPrefabs.Add(prefab.gameObject);
                SessionCollectionCache.Add(prefab.GetInstanceID());
            }
            else {
                var newGameCollection = CreateCollectionByPath(data.Path);
                if (newGameCollection == null) return;
                newGameCollection.networkPrefabs.Add(prefab.gameObject);
            }
        }
    }

    [CanBeNull]
    private static NetworkPrefabCollection CreateCollectionByPath(string path) {
        Debug.Log("create by path: " + path);
        var pathParts = path.Split("/");
        var fullPath = string.Empty;
        if (pathParts.Contains("AirshipPackages")) {
            // This asset belongs in the package's network prefab collection, create at package root.
            // Example:
            // pathParts[0] = Assets
            // pathParts[1] = AirshipPackages
            // pathParts[2] = @OrgName (IE: @Robbie)
            // pathParts[3] = PackageName (IE: NetworkSync)
            fullPath = $"Assets/AirshipPackages/{pathParts[2]}/{pathParts[3]}";
        }
        else {
            // Otherwise, create the game's network prefab collection at the root of the resource
            // folder.
            fullPath = "Assets/Resources";
        }
        var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{fullPath}/NetworkPrefabCollection.asset");
        if (!Directory.Exists(fullPath)) {
            // We shouldn't be here...
            return null;
        }
        // We can safely create the new collection and populate the asset database.
        var newCollection = ScriptableObject.CreateInstance<NetworkPrefabCollection>();
        AssetDatabase.CreateAsset(newCollection, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Resources.Load(assetPath);
        return newCollection;
    }
    
    
}
#endif
