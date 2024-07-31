using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Mirror;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NetworkPrefabLoader
{
    private HashSet<uint> loadedCollectionIds = new();

    private void Log(string s) {
        Debug.Log("[NetworkPrefabLoader]: " + s);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="bundle"></param>
    /// <param name="netCollectionId">Unique ID to group all FishNet NetworkObjects found in this bundle. This MUST be unique per-bundle and the same on server and client.</param>
    /// <returns></returns>
    public IEnumerator LoadNetworkObjects(AssetBundle bundle, ushort netCollectionId) {
        if (bundle.name.Contains("/scenes")) {
            yield break;
        }

        // this.Log("Loading network objects in bundle \"" + bundle.name + "\" into netCollectionId " + netCollectionId);

        // SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(netCollectionId, true);
        // List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();

        var st = Stopwatch.StartNew();

        string networkPrefabCollectionPath = null;
        foreach (var path in bundle.GetAllAssetNames()) {
            if (path.EndsWith("networkprefabcollection.asset")) {
                Debug.Log("Found a collection: " + path);
                networkPrefabCollectionPath = path;
                break;
            }
        }

        if (networkPrefabCollectionPath == null) {
            yield break;
        }

        var networkPrefabCollectionRequest = bundle.LoadAssetAsync<NetworkPrefabCollection>(networkPrefabCollectionPath);
        yield return networkPrefabCollectionRequest;
        var networkPrefabCollection = (NetworkPrefabCollection) networkPrefabCollectionRequest.asset;
        if (networkPrefabCollection) {
            // List<AssetBundleRequest> loadList = new(networkPrefabCollection.networkPrefabs.Count);
            // foreach (var prefab in networkPrefabCollection.networkPrefabs) {
            //     this.Log("Loading GameObject " + prefab.name);
            //     loadList.Add(bundle.LoadAssetAsync<GameObject>(prefab.name));
            // }

            // yield return loadList.ToArray().GetEnumerator();
            
            
            // When we are in a client build and a remote server.
            uint nobCounter = 0;
            int skipped = 0;
            foreach (var asset in networkPrefabCollection.networkPrefabs) {
                if (asset is GameObject go) {
                    // NetworkClient.RegisterPrefab(go, (uint)netCollectionId * 1000 + nobCounter);
                    NetworkClient.RegisterPrefab(go);
                } else {
                    skipped++;
                }
            }

            this.loadedCollectionIds.Add(netCollectionId);
            this.Log($"Finished loading {nobCounter} NetworkObject{(nobCounter != 1 ? "s" : "")} for \"" + bundle + "\" in " + st.ElapsedMilliseconds + "ms. Skipped " + skipped + " entries.");
        }
    }
    
    public void UnloadAll() {
        var toRemove = new List<GameObject>();
        foreach (var pair in NetworkClient.prefabs) {
            uint collectionId = pair.Key / 1000;
            if (this.loadedCollectionIds.Contains(collectionId)) {
                toRemove.Add(pair.Value);
            }
        }

        foreach (var prefab in toRemove) {
            NetworkClient.UnregisterPrefab(prefab);
        }
        this.loadedCollectionIds.Clear();
        Debug.Log("Unregistered " + toRemove.Count + " network prefabs.");
    }

    public void UnloadNetCollectionId(ushort collectionId) {
        var toRemove = new List<GameObject>();
        foreach (var pair in NetworkClient.prefabs) {
            uint id = pair.Key / 1000;
            if (id == collectionId) {
                toRemove.Add(pair.Value);
            }
        }

        foreach (var prefab in toRemove) {
            NetworkClient.UnregisterPrefab(prefab);
        }
        Debug.Log("Unregistered " + toRemove.Count + " network prefabs in collection " + collectionId);
        this.loadedCollectionIds.Remove(collectionId);
    }
}