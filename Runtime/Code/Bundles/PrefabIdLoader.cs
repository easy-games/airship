using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

public class PrefabIdLoader
{
    // private AsyncOperationHandle<IList<GameObject>> _asyncHandle;
    // Dictionary used to store the Id of each addressables package.
    // This is a representation of you tracking Ids.
    private Dictionary<string, ushort> _addressableIds = new Dictionary<string, ushort>();

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

        Debug.Log("Loading bundle " + bundle.name);

        SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(netCollectionId, true);
        List<NetworkObject> cache = new List<NetworkObject>();

        Profiler.BeginSample("LoadNetworkObjects");
        var st = Stopwatch.StartNew();

        var networkPrefabCollectionRequest = bundle.LoadAssetAsync<NetworkPrefabCollection>("NetworkPrefabCollection.asset");
        yield return networkPrefabCollectionRequest;
        var networkPrefabCollection = (NetworkPrefabCollection) networkPrefabCollectionRequest.asset;
        if (networkPrefabCollection) {
            List<AssetBundleRequest> loadList = new(networkPrefabCollection.networkPrefabs.Count);
            foreach (var prefab in networkPrefabCollection.networkPrefabs) {
                Debug.Log("Loading " + prefab.name);
                loadList.Add(bundle.LoadAssetAsync<GameObject>(prefab.name));
            }

            yield return loadList.ToArray().GetEnumerator();
            foreach (var loadResult in loadList) {
                var asset = loadResult.asset;
                if (asset is GameObject go) {
                    Debug.Log("Loading NOB " + asset.name);
                    if (go.TryGetComponent(typeof(NetworkObject), out Component nob)) {
                        cache.Add((NetworkObject)nob);
                    }
                }
            }

            foreach (var loadResult in loadList) {
                var asset = loadResult.asset;
                if (asset is DynamicVariables vars) {
                    Debug.Log("Registering Dynamic Variables Collection id=" + vars.collectionId);
                    DynamicVariablesManager.Instance.RegisterVars(vars.collectionId, vars);
                }
            }
            spawnablePrefabs.AddObjects(cache);

            Debug.Log("LoadAllAssets for " + bundle + ": " + st.ElapsedMilliseconds + "ms.");
            Profiler.EndSample();
        }
    }
    
    public void UnloadBundle(string key)
    {
        //Get the Id of your addressables package.
        ushort id = _addressableIds[key];
        //Once again get the prefab collection for Id.
        SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(id, true);
        spawnablePrefabs.Clear();

        // Addressables.Release(_asyncHandle);
    }
}