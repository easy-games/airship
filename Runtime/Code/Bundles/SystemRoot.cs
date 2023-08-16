using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Code.Bootstrap;
using Code.GameBundle;
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
	public ushort networkCollectionIdCounter = 1;

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

	public IEnumerator LoadBundles(string gameId, EasyEditorConfig editorConfig, List<AirshipPackage> packages)
	{
		var sw = Stopwatch.StartNew();

		List<IEnumerator> loadList1 = new();

		var useBundles = IsUsingBundles(editorConfig);
		AssetBridge.useBundles = useBundles;
		print("is using bundles: " + useBundles);
		if (useBundles)
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


		Debug.Log("Finished loading asset bundles in " + sw.ElapsedMilliseconds + "ms");
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

	private IEnumerator LoadSingleAssetBundleFromAirshipPackage(AirshipPackage airshipPackage, string assetBundleFile, ushort netCollectionId) {
		string bundleFilePath = Path.Join(airshipPackage.GetBuiltAssetBundleDirectory(), AirshipPlatformUtil.GetLocalPlatform().ToString(), assetBundleFile);

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

#if UNITY_SERVER
		Debug.Log($"Listing files for {airshipPackage.id}/{assetBundleFile}:");
		var files = assetBundle.GetAllAssetNames();
		foreach (var file in files) {
			Debug.Log("	- " + file);
		}
		Debug.Log("");
#endif

		var loadedAssetBundle = new LoadedAssetBundle(airshipPackage, assetBundleFile, assetBundle);
		loadedAssetBundles.Add(airshipPackage.id + "_" + assetBundleFile, loadedAssetBundle);

		yield return _prefabIdLoader.LoadNetworkObjects(assetBundle, netCollectionId);
	}
}
