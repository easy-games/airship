using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Code.PoolManager {

	[LuauAPI]
	[Preserve]
	public class PoolManager : Singleton<PoolManager>
	{
		public bool logStatus;
		public Transform root;

		private Dictionary<GameObject, ObjectPool<GameObject>> prefabLookup;
		private Dictionary<GameObject, ObjectPool<GameObject>> instanceLookup;

		private bool dirty = false;

		void Awake () {
			root = transform;
			prefabLookup = new Dictionary<GameObject, ObjectPool<GameObject>>();
			instanceLookup = new Dictionary<GameObject, ObjectPool<GameObject>>();
		}

		void Update()
		{
			if(logStatus && dirty)
			{
				PrintStatus();
				dirty = false;
			}
		}

		public void InternalPreLoadPool(GameObject prefab, int size)
		{
			if(prefabLookup.ContainsKey(prefab))
			{
				Debug.LogError("Pool for prefab " + prefab.name + " has already been created");
				return;
			}
			var pool = new ObjectPool<GameObject>(() => { return InstantiatePrefab(prefab); }, size);
			prefabLookup[prefab] = pool;
			StartCoroutine(pool.Warm(size));

			dirty = true;
		}

		public GameObject InternalSpawnObject(GameObject prefab)
		{
			return this.InternalSpawnObject(prefab, prefab.transform.localPosition, prefab.transform.localRotation);
		}

		public GameObject InternalSpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			if (!prefabLookup.ContainsKey(prefab))
			{
				this.InternalPreLoadPool(prefab, 1);
			}

			var pool = prefabLookup[prefab];

			var clone = pool.GetItem();
			clone.transform.SetParent(parent);
			clone.transform.SetLocalPositionAndRotation(position, rotation);
			clone.SetActive(true);

			instanceLookup.Add(clone, pool);
			dirty = true;
			return clone;
		}

		public void InternalReleaseObject(GameObject clone)
		{
			clone.transform.SetParent(root);
			clone.SetActive(false);

			if(instanceLookup.ContainsKey(clone))
			{
				instanceLookup[clone].ReleaseItem(clone);
				instanceLookup.Remove(clone);
				dirty = true;
			}
			else
			{
				// Debug.LogWarning("No pool contains the object: " + clone.name);
			}
		}


		private GameObject InstantiatePrefab(GameObject prefab)
		{
			var go = Instantiate(prefab, this.root) as GameObject;
			go.SetActive(false);
			return go;
		}

		public void PrintStatus()
		{
			foreach (KeyValuePair<GameObject, ObjectPool<GameObject>> keyVal in prefabLookup)
			{
				Debug.Log(string.Format("Object Pool for Prefab: {0} In Use: {1} Total {2}", keyVal.Key.name, keyVal.Value.CountUsedItems, keyVal.Value.Count));
			}
		}

		#region Static API

		public static void PreLoadPool(GameObject prefab, int size)
		{
			Instance.InternalPreLoadPool(prefab, size);
		}

		public static GameObject SpawnObject(GameObject prefab)
		{
			return Instance.InternalSpawnObject(prefab);
		}

		public static GameObject SpawnObject(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation)
		{
			return Instance.InternalSpawnObject(prefab, worldPosition, worldRotation);
		}
		
		public static GameObject SpawnObject(GameObject prefab, Transform parent)
		{
			return Instance.InternalSpawnObject(prefab, prefab.transform.localPosition, prefab.transform.localRotation, parent);
		}

		public static GameObject SpawnObject(GameObject prefab, Vector3 localPosition, Quaternion localRotation, Transform parent)
		{
			return Instance.InternalSpawnObject(prefab, localPosition, localRotation, parent);
		}

		public static void ReleaseObject(GameObject clone)
		{
			Instance.InternalReleaseObject(clone);
		}

		#endregion
	}
}