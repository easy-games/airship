using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using JetBrains.Annotations;
using UnityEngine;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;

public class SystemRoot : Singleton<SystemRoot> {
public Dictionary<string, LoadedAssetBundle> loadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

	private PrefabIdLoader _prefabIdLoader = new PrefabIdLoader();

	private void Start()
	{
		DontDestroyOnLoad(this);
	}

	public bool IsUsingBundles([CanBeNull] EasyEditorConfig editorConfig)
	{
		bool useBundles = true;
		if (Application.isEditor)
		{
			useBundles = false;
			if (editorConfig != null && editorConfig.useBundlesInEditor)
			{
				useBundles = true;
			}

			if (!CrossSceneState.IsLocalServer() && !CrossSceneState.UseLocalBundles)
			{
				useBundles = true;
			}
		}

		return useBundles;
	}

	public IEnumerator LoadBundles(string game, EasyEditorConfig editorConfig)
	{
		var sw = Stopwatch.StartNew();

		var coreBundleRootPath = Path.Combine(BootstrapHelper.ImportsBundleRelativeRootPath, "core");
		var gameBundleRootPath = BootstrapHelper.GameBundleRelativeRootPath;

		List<IEnumerator> loadList1 = new();
		List<IEnumerator> loadList2 = new();

		// Resources
		var useBundles = IsUsingBundles(editorConfig);
		AssetBridge.useBundles = useBundles;
		print("is using bundles: " + useBundles);
		if (useBundles)
		{
			if (RunCore.IsClient())
			{
				loadList1.Add(LoadAssetBundle("core", "client/resources", true,1));
				loadList1.Add(LoadAssetBundle("bedwars", "client/resources", false,2));
			}
			if (RunCore.IsServer())
			{
				loadList1.Add(LoadAssetBundle("core", "server/resources", true,3));
				loadList1.Add(LoadAssetBundle("bedwars", "server/resources", false,4));
			}
			loadList1.Add(LoadAssetBundle("core", "shared/resources", true,5));
			loadList1.Add(LoadAssetBundle("bedwars", "shared/resources", false,6));

			// Scenes
			loadList1.Add(LoadAssetBundle("core", "shared/scenes", true,7));
			loadList1.Add(LoadAssetBundle("bedwars", "shared/scenes", false,8));
			if (RunCore.IsServer())
			{
				loadList1.Add(LoadAssetBundle("core", "server/scenes", true,9));
				loadList1.Add(LoadAssetBundle("bedwars", "server/scenes", false,10));
			}
			if (RunCore.IsClient())
			{
				loadList1.Add(LoadAssetBundle("core", "client/scenes", true,11));
				loadList1.Add(LoadAssetBundle("bedwars", "client/scenes", false,12));
			}

			yield return this.WaitAll(loadList1.ToArray());
			yield return this.WaitAll(loadList2.ToArray());
		}
		else
		{
			var spawnablePrefabs = (SinglePrefabObjects)InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(1, true);
			var cache = new List<NetworkObject>();
			var assets = Resources.LoadAll<GameObject>("");
			foreach (GameObject obj in assets)
			{
				if (obj.TryGetComponent(typeof(NetworkObject), out Component nob))
				{
					cache.Add((NetworkObject)nob);
				}
			}
			spawnablePrefabs.AddObjects(cache);
		}

		// Debug SpawnablePrefabs
		// Debug.Log("----- Network Objects -----");
		// foreach (var collectionId in InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs.Keys)
		// {
		// 	var singlePrefabObjects = (SinglePrefabObjects)InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs[collectionId];
		// 	for (int i = 0; i < singlePrefabObjects.Prefabs.Count; i++)
		// 	{
		// 		var nob = singlePrefabObjects.Prefabs[i];
		// 		Debug.Log(nob.gameObject.name + " collId=" + collectionId + " objectId=" + nob.ObjectId);
		// 	}
		// }
		// Debug.Log("----------");


		Debug.Log("Finished loading asset bundles in " + sw.ElapsedMilliseconds + "ms.");
	}

	public void UnloadBundles()
	{
		foreach (var pair in loadedAssetBundles)
		{
			pair.Value.assetBundle.Unload(true);
			pair.Value.assetBundle = null;
		}
		loadedAssetBundles.Clear();
	}

	private IEnumerator LoadAssetBundle(string bundleId, string bundleFolder, bool isImport, ushort netCollectionId) {
		string bundleFilePath;
		if (isImport) {
			bundleFilePath = Path.Combine(AssetBridge.BundlesPath, "imports", bundleId, bundleFolder);
		} else {
			bundleFilePath = Path.Combine(AssetBridge.BundlesPath, bundleId, bundleFolder);
		}

		if (!File.Exists(bundleFilePath)) {
			Debug.Log($"Bundle file did not exist \"{bundleFilePath}. skipping.");
			yield break;
		}

		var st = Stopwatch.StartNew();
		var bundleCreateRequest = AssetBundle.LoadFromFileAsync(bundleFilePath);
		yield return bundleCreateRequest;
		Debug.Log($"Loaded AssetBundle {bundleId}/{bundleFolder} from file in {st.ElapsedMilliseconds}ms");

		var assetBundle = bundleCreateRequest.assetBundle;
		if (assetBundle == null)
		{
			Debug.LogError($"AssetBundle failed to load. name: {bundleId}/{bundleFolder}, bundleFilePath: {bundleFilePath}");
		}

		var loadedAssetBundle = new LoadedAssetBundle(bundleId, bundleFolder, isImport, assetBundle);
		loadedAssetBundles.Add(bundleId + "_" + bundleFolder, loadedAssetBundle);

		yield return _prefabIdLoader.LoadNetworkObjects(assetBundle, netCollectionId);
	}
}
