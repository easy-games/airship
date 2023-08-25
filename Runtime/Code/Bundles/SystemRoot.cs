using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Code.Bootstrap;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using JetBrains.Annotations;
using UnityEngine;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;

public class SystemRoot : Singleton<SystemRoot> {
	public Dictionary<string, LoadedAssetBundle> loadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

	private NetworkPrefabLoader networkNetworkPrefabLoader = new NetworkPrefabLoader();
	public ushort networkCollectionIdCounter = 1;

	private void Awake() {
		DontDestroyOnLoad(this);
		gameObject.hideFlags = HideFlags.DontSave;
	}

	public bool IsUsingBundles([CanBeNull] AirshipEditorConfig editorConfig)
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

	public IEnumerator LoadPackages(List<AirshipPackage> packages, bool useUnityAssetBundles, bool unloadOthers = true) {
		var sw = Stopwatch.StartNew();

		// Find packages we should UNLOAD
		List<string> unloadList = new();
		foreach (var loadedPair in this.loadedAssetBundles) {
			var packageToLoad = packages.Find(p => p.id == loadedPair.Value.airshipPackage.id);
			if (packageToLoad != null && packageToLoad.version != loadedPair.Value.airshipPackage.version) {
				unloadList.Add(loadedPair.Key);
			} else if (packageToLoad == null && unloadOthers) {
				unloadList.Add(loadedPair.Key);
			}
		}
		foreach (var bundleId in unloadList) {
			var loadedBundle = this.loadedAssetBundles[bundleId];
			this.UnloadBundle(loadedBundle);
		}

		// Reset state
		this.networkCollectionIdCounter = 1;

		// Find packages to load
		List<IEnumerator> loadList1 = new();
		AssetBridge.useBundles = useUnityAssetBundles;
		print("is using bundles: " + useUnityAssetBundles);
		if (useUnityAssetBundles)
		{
			// Resources
			foreach (var package in packages) {
				if (RunCore.IsClient()) {
					loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "client/resources", this.networkCollectionIdCounter));
				}
				this.networkCollectionIdCounter++;
			}
			foreach (var package in packages) {
				if (RunCore.IsServer()) {
					loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "server/resources", this.networkCollectionIdCounter));
				}
				this.networkCollectionIdCounter++;
			}
			foreach (var package in packages) {
				loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "shared/resources", this.networkCollectionIdCounter));
				this.networkCollectionIdCounter++;
			}

			// Scenes
			foreach (var package in packages) {
				loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "shared/scenes", this.networkCollectionIdCounter));
				this.networkCollectionIdCounter++;
			}
			foreach (var package in packages) {
				if (RunCore.IsServer()) {
					loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "server/scenes", this.networkCollectionIdCounter));
				}
				this.networkCollectionIdCounter++;
			}
			foreach (var package in packages) {
				if (RunCore.IsClient()) {
					loadList1.Add(LoadSingleAssetBundleFromAirshipPackage(package, "client/scenes", this.networkCollectionIdCounter));
				}
				this.networkCollectionIdCounter++;
			}

			yield return this.WaitAll(loadList1.ToArray());
		}
		else
		{
			if (InstanceFinder.NetworkManager != null && !InstanceFinder.NetworkManager.IsOffline) {
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
		}

		// Debug SpawnablePrefabs
		if (InstanceFinder.NetworkManager != null && !InstanceFinder.NetworkManager.IsOffline) {
			Debug.Log("----- Network Objects -----");
			foreach (var collectionId in InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs.Keys)
			{
				var singlePrefabObjects = (SinglePrefabObjects)InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs[collectionId];
				for (int i = 0; i < singlePrefabObjects.Prefabs.Count; i++)
				{
					var nob = singlePrefabObjects.Prefabs[i];
					Debug.Log(nob.gameObject.name + " collId=" + collectionId + " objectId=" + nob.ObjectId);
				}
			}
			Debug.Log("----------");
		}


		Debug.Log("Finished loading asset bundles in " + sw.ElapsedMilliseconds + "ms");
	}

	public void UnloadAllBundles() {
		var st = Stopwatch.StartNew();
		foreach (var pair in loadedAssetBundles)
		{
			pair.Value.assetBundle.Unload(true);
			pair.Value.assetBundle = null;
		}
		loadedAssetBundles.Clear();
		this.networkNetworkPrefabLoader.UnloadAll();
		this.networkCollectionIdCounter = 1;
		Debug.Log($"Unloaded asset bundles in {st.ElapsedMilliseconds} ms.");
	}

	public void UnloadBundle(LoadedAssetBundle loadedBundle) {
		Debug.Log($"[SystemRoot]: Unloading bundle {loadedBundle.bundleId}/{loadedBundle.assetBundleFile}");
		loadedBundle.assetBundle.Unload(true);
		loadedBundle.assetBundle = null;
		var key = SystemRoot.GetLoadedAssetBundleKey(loadedBundle.airshipPackage, loadedBundle.assetBundleFile);
		loadedAssetBundles.Remove(key);
		this.networkNetworkPrefabLoader.UnloadNetCollectionId(loadedBundle.netCollectionId);
	}

	public static string GetLoadedAssetBundleKey(AirshipPackage package, string assetBundleFile) {
		return package.id + "_" + assetBundleFile;
	}

	private IEnumerator LoadSingleAssetBundleFromAirshipPackage(AirshipPackage airshipPackage, string assetBundleFile, ushort netCollectionId) {
		// ReSharper disable once ReplaceWithSingleAssignment.True
		bool doNetworkPrefabLoading = true;
		if (InstanceFinder.IsOffline && RunCore.IsClient()) {
			doNetworkPrefabLoading = false;
		}

		string assetBundleId = GetLoadedAssetBundleKey(airshipPackage, assetBundleFile);
		if (this.loadedAssetBundles.ContainsKey(assetBundleId)) {
			Debug.Log($"AssetBundle \"{assetBundleId}\" was already loaded. Skipping load.");
			var existingBundle = this.loadedAssetBundles[assetBundleId];
			if (doNetworkPrefabLoading) {
				existingBundle.netCollectionId = netCollectionId;
				yield return networkNetworkPrefabLoader.LoadNetworkObjects(existingBundle.assetBundle, netCollectionId);
			}
			yield break;
		}

		string bundleFilePath = Path.Join(airshipPackage.GetBuiltAssetBundleDirectory(AirshipPlatformUtil.GetLocalPlatform()), assetBundleFile);

		if (!File.Exists(bundleFilePath)) {
			Debug.Log($"Bundle file did not exist \"{bundleFilePath}. skipping.");
			yield break;
		}

		var st = Stopwatch.StartNew();
		var bundleCreateRequest = AssetBundle.LoadFromFileAsync(bundleFilePath);
		yield return bundleCreateRequest;
		Debug.Log($"Loaded AssetBundle {airshipPackage.id}/{assetBundleFile} from file in {st.ElapsedMilliseconds}ms");

		var assetBundle = bundleCreateRequest.assetBundle;
		if (assetBundle == null)
		{
			Debug.LogError($"AssetBundle failed to load. name: {airshipPackage.id}/{assetBundleFile}, bundleFilePath: {bundleFilePath}");
			yield break;
		}

// #if UNITY_SERVER
// 		Debug.Log($"Listing files for {airshipPackage.id}/{assetBundleFile}:");
// 		var files = assetBundle.GetAllAssetNames();
// 		foreach (var file in files) {
// 			Debug.Log("	- " + file);
// 		}
// 		Debug.Log("");
// 		Debug.Log($"Listing scenes for {airshipPackage.id}/{assetBundleFile}:");
// 		foreach (var scene in assetBundle.GetAllScenePaths()) {
// 			Debug.Log("  - " + scene);
// 		}
// 		Debug.Log("");
// #endif

		var loadedAssetBundle = new LoadedAssetBundle(airshipPackage, assetBundleFile, assetBundle, netCollectionId);
		loadedAssetBundles.Add(assetBundleId, loadedAssetBundle);

		if (doNetworkPrefabLoading) {
			yield return networkNetworkPrefabLoader.LoadNetworkObjects(assetBundle, netCollectionId);
		} else {
			Debug.Log("Operating in offline context. Skipping network prefab loading.");
		}
	}
}
