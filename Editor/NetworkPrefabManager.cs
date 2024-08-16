#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Mirror;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        return Path.StartsWith("Packages", StringComparison.OrdinalIgnoreCase) 
               || (Path.StartsWith("Assets/AirshipPackages/@Easy/Core") && !IsLocalPackageAsset())
               || Path.Contains("gg.easy.airship");
    }

    public bool IsGameAsset() {
        return OrgName == String.Empty || PackageName == String.Empty;
    }

    public bool IsLocalPackageAsset() {
        // It's important to know if a package asset belongs to one of _our_ local packages,
        // or an external package. We don't need to manage external package assets, external
        // packages will handle their own assets.
#if UNITY_EDITOR
        if (OrgName == String.Empty || PackageName == String.Empty) return false;
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) {
            Debug.LogWarning("Failed IsLocalPackageAsset() check because GameConfig was null.");
            return false;
        }
        var packages = gameConfig.packages;
        foreach (var package in packages) {
            var idParts = package.id.Split("/");
            var pOrg = idParts[0];
            var pName = idParts[1];
            if (OrgName == pOrg && PackageName == pName && package.localSource) {
                return true;
            }
        }
#endif
        return false;
    }
    
}

public class NetworkPrefabManager : AssetPostprocessor {

    // `HashSet` of instance ids of instances that are already inside a collection.
    private static readonly HashSet<string> SessionCollectionCache = new HashSet<string>();

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        string[] movedFromAssetPaths, bool didDomainReload) {
        if (!didDomainReload) return;
        
        // Register existing prefabs in session cache   
        RefreshSessionCollectionCache();
        
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.ExitingEditMode) {
            WriteAllCollections();
        }
    }

    private static AssetData GetAssetDataFromPath(string path) {
        return new AssetData(path);
    }

    private static void RefreshSessionCollectionCache() {
        SessionCollectionCache.Clear();
        foreach (var collection in GetCollections()) {
            foreach (var prefab in collection.networkPrefabs) {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out var guid, out var localId);
                SessionCollectionCache.Add(guid);
            }
        }
    }
    
    private static List<NetworkPrefabCollection> GetCollections() {
        var validCollections = new List<NetworkPrefabCollection>();
        var results = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
        foreach (var result in results) {
            var path = AssetDatabase.GUIDToAssetPath(result);
            var assetData = GetAssetDataFromPath(path);
            if(assetData.IsInternalAsset()) continue;
            var collection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
            validCollections.Add(collection);

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

    private static List<GameObject> GetNetworkObjects() {
        var results = AssetDatabase.FindAssets("t:prefab");
        var networkObjects = new List<GameObject>();
        foreach (var result in results) {
            // We can rule out certain prefabs by simply looking at the path,
            // this gets eliminates _many_ `LoadAssetAtPath` calls, which
            // is the most expensive part of this process.
            var path = AssetDatabase.GUIDToAssetPath(result);
            var assetData = GetAssetDataFromPath(path);
            if (assetData.IsInternalAsset()) continue;
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // todo: change to NetworkIdentity
            var maybeNob = loaded.GetComponent<NetworkIdentity>();
            if (maybeNob == null) continue;

            networkObjects.Add(loaded);
        }
        return networkObjects;
    }

    private static void ClearAllCollections() {
        var collections = GetCollections();
        foreach (var collection in collections) {
            collection.networkPrefabs.Clear();
        }
    }

    [MenuItem("Airship/Misc/Generate Network Prefab Collections")]
    public static void WriteAllCollections() {
        // Refresh collection cache
        RefreshSessionCollectionCache();
        
        var nobs = GetNetworkObjects();
        HashSet<UnityEngine.Object> modifiedCollections = new();
        foreach (var nob in nobs) {
            var assetPath = AssetDatabase.GetAssetPath(nob);
            var assetData = GetAssetDataFromPath(assetPath);
            WriteToCollection(nob, assetData, modifiedCollections);
        }

        // Save ALL collections.
        foreach (var collection in modifiedCollections) {
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
        }
    }

    private static void WriteToCollection(GameObject nob, AssetData data, HashSet<UnityEngine.Object> modifiedCollections) {
        // nob.airshipGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(nob.gameObject)).ToString();

        var prefab = nob.gameObject;
        var isNested = prefab.transform.parent != null;
        // Nested NOB, no need to process this, the root NOB will live in the collection.
        if (isNested) return;
        // var instanceId = prefab.GetInstanceID();
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out var prefabGuid, out var localId);
        var alreadyInCollection = SessionCollectionCache.Contains(prefabGuid);
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
                modifiedCollections.Add(packageCollection);
                SessionCollectionCache.Add(prefabGuid);
            }
            else {
                var newPackageCollection = CreateCollectionByPath(data.Path);
                if (newPackageCollection == null) return;
                newPackageCollection.networkPrefabs.Add(prefab.gameObject);
                modifiedCollections.Add(newPackageCollection);
                SessionCollectionCache.Add(prefabGuid);
            }
        }
        if (data.IsGameAsset()) {
            // This doesn't belong to a package, this asset should be inside of
            // the game's network prefab collection.
            var gameCollection = GetGameCollection();
            if (gameCollection) {
                gameCollection.networkPrefabs.Add(prefab.gameObject);
                modifiedCollections.Add(gameCollection);
                SessionCollectionCache.Add(prefabGuid);
            }
            else {
                var newGameCollection = CreateCollectionByPath(data.Path);
                if (newGameCollection == null) return;
                newGameCollection.networkPrefabs.Add(prefab.gameObject);
                modifiedCollections.Add(newGameCollection);
                SessionCollectionCache.Add(prefabGuid);
            }
        }
    }

    [CanBeNull]
    private static NetworkPrefabCollection CreateCollectionByPath(string path) {
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
        return newCollection;
    }
}
#endif
