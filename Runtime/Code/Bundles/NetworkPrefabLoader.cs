using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NetworkPrefabLoader
{
    private HashSet<ushort> loadedCollectionIds = new();

    private void Log(string s) {
        Debug.Log("[NetworkPrefabLoader]: " + s);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="bundle"></param>
    /// <param name="netCollectionId">Unique ID to group all FishNet NetworkObjects found in this bundle. This MUST be unique per-bundle and the same on server and client.</param>
    /// <returns></returns>
    public IEnumerator LoadNetworkObjects(AssetBundle bundle, ushort netCollectionId)
    {
        if (bundle.name.Contains("/scenes")) {
            yield break;
        }

        // this.Log("Loading network objects in bundle \"" + bundle.name + "\" into netCollectionId " + netCollectionId);

        SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(netCollectionId, true);
        List<NetworkObject> cache = new List<NetworkObject>();

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
            var prefabIndex = 0;
            foreach (var asset in networkPrefabCollection.networkPrefabs) {
                if (asset is GameObject go) {
                    this.Log("Loading NetworkObject " + asset.name + " --- " + netCollectionId + " ---- " + go.name + "-----" + go.GetInstanceID());
                    if (go.TryGetComponent(typeof(NetworkObject), out Component nob)) {
                        var prefab = (NetworkObject)nob;
                        //ManagedObjects.InitializePrefab(prefab, prefabIndex, netCollectionId);
                        cache.Add(prefab);
                    }
                } else if (asset is DynamicVariables vars) {
                    // this.Log("Registering Dynamic Variables Collection id=" + vars.collectionId);
                    DynamicVariablesManager.Instance.RegisterVars(vars.collectionId, vars);
                }

                prefabIndex++;
            }

            spawnablePrefabs.AddObjects(cache);
            CollectionCaches<NetworkObject>.Store(cache);

            this.loadedCollectionIds.Add(netCollectionId);

            this.Log("Finished loading network objects for \"" + bundle + "\" in " + st.ElapsedMilliseconds + "ms.");
        }
    }
    
    public void UnloadAll()
    {
        foreach (var collectionId in this.loadedCollectionIds) {
            //Once again get the prefab collection for Id.
            SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(collectionId, true);
            spawnablePrefabs.Clear();
        }
        this.loadedCollectionIds.Clear();
    }

    public void UnloadNetCollectionId(ushort collectionId) {
        SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager?.GetPrefabObjects<SinglePrefabObjects>(collectionId, true);
        if (spawnablePrefabs) {
            spawnablePrefabs.Clear();
        }
        this.loadedCollectionIds.Remove(collectionId);
    }
}