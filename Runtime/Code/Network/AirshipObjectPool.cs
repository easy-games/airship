using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Utility.Performance;
using UnityEngine;

namespace Code.Network {
    public class AirshipObjectPool : DefaultObjectPool {
        public int maxSpawnPerFrame = 5;

        public void SlowlyCacheObjects(NetworkObject prefab, int count) {
            if (count <= 0)
                return;
            if (prefab == null)
                return;
            if (prefab.PrefabId == NetworkObject.UNSET_PREFABID_VALUE)
            {
                InstanceFinder.NetworkManager.LogError($"Prefab {prefab.name} has an invalid prefabId and cannot be cached.");
                return;
            }

            StartCoroutine(StartSlowSpawn(prefab, count));
        }

        public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, Vector3 position, Quaternion rotation, bool asServer) {
            var obj = base.RetrieveObject(prefabId, collectionId, position, rotation, asServer);
            obj.transform.SetParent(null);
            return obj;
        }

        private IEnumerator StartSlowSpawn(NetworkObject prefab, int count) {
            Stack<NetworkObject> cache = base.GetOrCreateCache(prefab.SpawnableCollectionId, prefab.PrefabId);
            for (int i = 0; i < count; i++)
            {
                NetworkObject nob = Instantiate(prefab, this.transform);
                nob.gameObject.SetActive(false);
                cache.Push(nob);
                if (i % this.maxSpawnPerFrame == 0) {
                    yield return null;
                }
            }
        }
    }
}