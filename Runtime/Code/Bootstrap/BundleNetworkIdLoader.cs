using System;
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
        if (bundle.name.Contains("/scenes"))
        {
            yield return null;
        } else
        {
            Debug.Log("Loading bundle " + bundle.name);

            SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects) InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(netCollectionId, true);
            List<NetworkObject> cache = new List<NetworkObject>();

            Profiler.BeginSample("LoadNetworkObjects");
            var st = Stopwatch.StartNew();

            var loadAllRequest = bundle.LoadAllAssetsAsync(typeof(GameObject));
            yield return loadAllRequest;

            foreach (GameObject obj in loadAllRequest.allAssets)
            {
                if (obj.TryGetComponent(typeof(NetworkObject), out Component nob))
                {
                    cache.Add((NetworkObject) nob);
                }
            }

            Debug.Log("LoadAllAssets for " + bundle + ": " + st.ElapsedMilliseconds + "ms.");
            Profiler.EndSample();

            spawnablePrefabs.AddObjects(cache);
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