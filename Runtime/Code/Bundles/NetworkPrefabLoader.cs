using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Mirror;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NetworkPrefabLoader
{
    private Dictionary<uint, List<GameObject>> packageNetworkPrefabs = new();

    private void Log(string s) {
        Debug.Log("[NetworkPrefabLoader]: " + s);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="bundle"></param>
    /// <param name="netCollectionId">Unique ID to group all FishNet NetworkObjects found in this bundle. This MUST be unique per-bundle and the same on server and client.</param>
    /// <returns></returns>
    public IEnumerator RegisterNetworkObjects(AssetBundle bundle, ushort netCollectionId) {
        Debug.Log("register.1");
        if (bundle.name.Contains("/scenes")) {
            yield break;
        }

        // this.Log("Loading network objects in bundle \"" + bundle.name + "\" into netCollectionId " + netCollectionId);

        // SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(netCollectionId, true);
        // List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();

        var st = Stopwatch.StartNew();

        Debug.Log("register.2");
        string networkPrefabCollectionPath = null;
        foreach (var path in bundle.GetAllAssetNames()) {
            if (path.EndsWith("networkprefabcollection.asset")) {
                this.Log("Found a collection: " + path);
                networkPrefabCollectionPath = path;
                break;
            }
        }

        if (networkPrefabCollectionPath == null) {
            yield break;
        }

        Debug.Log("register.3");
        var networkPrefabCollectionRequest = bundle.LoadAssetAsync<NetworkPrefabCollection>(networkPrefabCollectionPath);
        yield return networkPrefabCollectionRequest;
        var networkPrefabCollection = (NetworkPrefabCollection) networkPrefabCollectionRequest.asset;
        if (networkPrefabCollection) {
            Debug.Log("register.3.1");
            // List<AssetBundleRequest> loadList = new(networkPrefabCollection.networkPrefabs.Count);
            // foreach (var prefab in networkPrefabCollection.networkPrefabs) {
            //     this.Log("Loading GameObject " + prefab.name);
            //     loadList.Add(bundle.LoadAssetAsync<GameObject>(prefab.name));
            // }

            // yield return loadList.ToArray().GetEnumerator();
            
            
            // When we are in a client build and a remote server.
            uint counter = 0;
            int skipped = 0;
            var added = new List<GameObject>();
            foreach (var asset in networkPrefabCollection.networkPrefabs) {
                if (asset is GameObject go) {
#if AIRSHIP_PLAYER || true
                    this.Log("Registering prefab: " + go.name);
#endif
                    NetworkClient.RegisterPrefab(go);
                    added.Add(go);
                    counter++;
                } else {
                    skipped++;
                }
            }

            this.packageNetworkPrefabs[netCollectionId] = added;
            this.Log($"Finished registering {counter} network prefab{(counter != 1 ? "s" : "")} for \"" + bundle + "\" in " + st.ElapsedMilliseconds + "ms.");
        }
    }

    public void UnloadNetCollectionId(ushort collectionId) {
        if (this.packageNetworkPrefabs.TryGetValue(collectionId, out var prefabs)) {
            int counter = 0;
            foreach (var prefab in prefabs) {
                NetworkClient.UnregisterPrefab(prefab);
                counter++;
            }

            this.packageNetworkPrefabs.Remove(collectionId);
            this.Log("Unregistered " + counter + " prefabs in collection " + collectionId);
        }
    }
}