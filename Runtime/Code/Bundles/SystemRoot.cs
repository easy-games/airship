using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Code.Bootstrap;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using JetBrains.Annotations;
using Luau;
using System;
using Airship.DevConsole;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class SystemRoot : Singleton<SystemRoot> {
	public Dictionary<string, LoadedAssetBundle> loadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

	public Dictionary<string, Dictionary<string, AirshipScript>> luauFiles = new();

	private NetworkPrefabLoader networkNetworkPrefabLoader = new NetworkPrefabLoader();
	public ushort networkCollectionIdCounter = 1;

	private void Awake() {
		DontDestroyOnLoad(this);
		// gameObject.hideFlags = HideFlags.DontSave;

		DevConsole.AddCommand(Command.Create<string>(
			"scripts",
			"",
			"Lists all scripts loaded from code.zip",
			Parameter.Create("package", "A package name or \"game\""),
			(packageName) => {
				if (packageName.ToLower() == "game") {
					foreach (var b in this.loadedAssetBundles) {
						if (b.Value.airshipPackage.packageType == AirshipPackageType.Game) {
							if (this.luauFiles.TryGetValue(b.Value.airshipPackage.id, out var gameScripts)) {
								int counter = 0;
								print(b.Value.airshipPackage.id + ":");
								foreach (var scriptPair in gameScripts) {
									Debug.Log("  - " + scriptPair.Key);
									counter++;
								}
								Debug.Log($"Listed {counter} scripts.");
								return;
							}
						}
					}
					Debug.LogError("There are no games loaded.");
					return;
				}

				if (!this.luauFiles.TryGetValue(packageName, out var pair)) {
					DevConsole.LogError($"Unable to find package named \"{packageName}\". All available packages:");
					foreach (var packagePair in this.luauFiles) {
						if (packagePair.Key.StartsWith("@")) {
							Debug.Log($"  - {packagePair.Key}");
						}
					}
					return;
				}

				int counter2 = 0;
				print(packageName + ":");
				foreach (var scriptPair in pair) {
					Debug.Log("  - " + scriptPair.Key);
					counter2++;
				}
				Debug.Log($"Listed {counter2} scripts.");
			},
			() => {
				int counter = 0;
				foreach (var pair in this.luauFiles) {
					print(pair.Key + ":");
					foreach (var scriptPair in pair.Value) {
						Debug.Log("  - " + scriptPair.Key);
						counter++;
					}
				}
				Debug.Log($"Listed {counter} scripts in {this.luauFiles.Count} bundles.");
			}
		));
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

	/// <summary>
	///
	/// </summary>
	/// <param name="packages"></param>
	/// <param name="useUnityAssetBundles"></param>
	/// <param name="forceUnloadAll">If false, we attempt to keep packages that are already loaded in place (instead of unloading and re-loading them)</param>
	/// <returns></returns>
	public IEnumerator LoadPackages(List<AirshipPackage> packages, bool useUnityAssetBundles, bool forceUnloadAll = true, bool compileLuaOnClient = false) {
#if AIRSHIP_PLAYER
		print("Packages to load:");
		for (int i = 0; i < packages.Count; i++) {
			print($"  {i}. {packages[i].id} (Assets v{packages[i].assetVersion}) (Code v{packages[i].codeVersion})");
		}
#endif
		//
		// print("Already loaded asset bundles:");
		// {
		// 	int i = 0;
		// 	foreach (var pair in this.loadedAssetBundles) {
		// 		print($"  {i}. {pair.Value.airshipPackage.id} v{pair.Value.airshipPackage.version} ({pair.Key})");
		// 		i++;
		// 	}
		// }

		// Find packages we should UNLOAD
		List<string> unloadList = new();
		foreach (var loadedPair in this.loadedAssetBundles) {
			if (forceUnloadAll) {
				unloadList.Add(loadedPair.Key);
				continue;
			}
			var packageToLoad = packages.Find(p => p.id.ToLower() == loadedPair.Value.airshipPackage.id.ToLower());
			if (packageToLoad == null || packageToLoad.assetVersion != loadedPair.Value.airshipPackage.assetVersion || packageToLoad.codeVersion != loadedPair.Value.airshipPackage.codeVersion) {
				unloadList.Add(loadedPair.Key);
			}
		}
		foreach (var bundleId in unloadList) {
			var loadedBundle = this.loadedAssetBundles[bundleId];
			this.UnloadBundle(loadedBundle);
		}

		// code.zip
		bool openCodeZips = RunCore.IsServer() || compileLuaOnClient;
#if !AIRSHIP_PLAYER
		if (Application.isEditor) {
			openCodeZips = false;
		}
#endif
		if (openCodeZips) {
			var st = Stopwatch.StartNew();
			int scriptCounter = 0;
			var binaryFileTemplate = ScriptableObject.CreateInstance<AirshipScript>();
			foreach (var package in packages) {
				var codeZipPath = Path.Join(package.GetPersistentDataDirectory(), "code.zip");
				if (File.Exists(codeZipPath)) {
					ZipArchive zip = null;
					try {
						zip = ZipFile.OpenRead(codeZipPath);
					} catch (Exception e) {
						Debug.LogError("Failed to open code.zip file: " + e);
					}
					if (zip == null) {
						Debug.LogError("Zip was null. This is bad.");
						yield break;
					}

					foreach (var entry in zip.Entries) {
						if (entry.Name.EndsWith("json~")) {
							continue;
						}

						// check for metadata json
						var jsonEntry = zip.GetEntry(entry.FullName + ".json~");
						bool airshipBehaviour = jsonEntry != null;

						using (var stream = entry.Open()) {
							using (var sr = new StreamReader(stream)) {
								var text = sr.ReadToEnd();
								var bf = Object.Instantiate(binaryFileTemplate);
								bf.m_metadata = null;
								bf.airshipBehaviour = false;
								LuauCompiler.RuntimeCompile(entry.FullName, text, bf, airshipBehaviour);
// #if UNITY_SERVER
								// print("Compiled " + entry.FullName + (!airshipBehaviour ? "" : " (AirshipBehaviour)") + " (package: " + package.id + ")");
// #endif
								this.AddLuauFile(package.id, bf);
								scriptCounter++;
							}
						}
					}
				} else {
#if AIRSHIP_PLAYER
					Debug.Log("code.zip not found for package " + package.id);
#endif
				}
			}
			Debug.Log($"Compiled {scriptCounter} Luau scripts in {st.ElapsedMilliseconds} ms.");
		}


		// Reset state
		// 0 is reserved for player prefab
		this.networkCollectionIdCounter = 1;

		// sort packages by load order
		List<List<IEnumerator>> loadLists = new(3);
		for (int i = 0; i < loadLists.Capacity; i++) {
			loadLists.Add(new());
		}

		List<IEnumerator> GetLoadList(AirshipPackage package) {
			return loadLists[0];
			// if (package.id == "@Easy/CoreMaterials") {
			// 	return loadLists[0];
			// }
			// if (package.id == "@Easy/Core") {
			// 	return loadLists[1];
			// }
			// return loadLists[2];
		}

		// Find packages to load
		AssetBridge.useBundles = useUnityAssetBundles;
		if (useUnityAssetBundles) {

			// Resources
			foreach (var package in packages) {
				GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "shared/resources", this.networkCollectionIdCounter));
				this.networkCollectionIdCounter++;

			}
			// foreach (var package in packages) {
			// 	GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "client/resources", this.networkCollectionIdCounter));
			// 	this.networkCollectionIdCounter++;
			// }
			foreach (var package in packages) {
				if (RunCore.IsServer()) {
					GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "server/resources", this.networkCollectionIdCounter));
				}
				this.networkCollectionIdCounter++;
			}

			// Scenes
			foreach (var package in packages) {
				GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "shared/scenes", this.networkCollectionIdCounter));
				this.networkCollectionIdCounter++;
			}
			// foreach (var package in packages) {
			// 	GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "client/scenes", this.networkCollectionIdCounter));
			// 	this.networkCollectionIdCounter++;
			// }
			// foreach (var package in packages) {
			// 	if (RunCore.IsServer()) {
			// 		GetLoadList(package).Add(LoadSingleAssetBundleFromAirshipPackage(package, "server/scenes", this.networkCollectionIdCounter));
			// 	}
			// 	this.networkCollectionIdCounter++;
			// }

			yield return this.WaitAll(loadLists[0].ToArray());
		} else {
			var st = Stopwatch.StartNew();
			if (InstanceFinder.NetworkManager != null && !InstanceFinder.NetworkManager.IsOffline) {
#if UNITY_EDITOR
				var spawnablePrefabs =
					(SinglePrefabObjects)InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(1, true);
				var cache = new List<NetworkObject>();

				var guids = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
				Array.Sort(guids);
				foreach (var guid in guids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var networkPrefabCollection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
					foreach (var obj in networkPrefabCollection.networkPrefabs) {
						if (obj is GameObject go) {
							if (go.TryGetComponent(typeof(NetworkObject), out Component nob)) {
								cache.Add((NetworkObject)nob);
							}
						}
					}
				}

				spawnablePrefabs.AddObjects(cache);
#endif
			}

		}

		
#if !UNITY_EDITOR || AIRSHIP_PLAYER
		if (InstanceFinder.NetworkManager != null && !InstanceFinder.NetworkManager.IsOffline) {
			Debug.Log("----- Network Objects -----");
			foreach (var collectionId in InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs.Keys)
			{
				var singlePrefabObjects = (SinglePrefabObjects)InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs[collectionId];
				for (int i = 0; i < singlePrefabObjects.Prefabs.Count; i++)
				{
					var nob = singlePrefabObjects.Prefabs[i];
					Debug.Log($"  - {collectionId}.{i} {nob.gameObject.name}");
				}
			}
			Debug.Log("--------------------------------------");
		}
#endif


#if AIRSHIP_DEBUG
		Debug.Log("[Airship]: Finished loading asset bundles in " + sw.ElapsedMilliseconds + "ms");
#endif
	}

	public void UnloadAllBundles() {
		var st = Stopwatch.StartNew();

		foreach (var pair in loadedAssetBundles) {
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
		// this.ClearLuauFiles(loadedBundle.airshipPackage.id);
		loadedBundle.assetBundle.Unload(true);
		loadedBundle.assetBundle = null;
		var key = SystemRoot.GetLoadedAssetBundleKey(loadedBundle.airshipPackage, loadedBundle.assetBundleFile);
		loadedAssetBundles.Remove(key);
		this.networkNetworkPrefabLoader.UnloadNetCollectionId(loadedBundle.netCollectionId);
	}

	public void AddLuauFile(string packageKey, AirshipScript br) {
		Dictionary<string, AirshipScript> files;
		if (!this.luauFiles.TryGetValue(packageKey, out files)) {
			files = new();
			this.luauFiles.Add(packageKey, files);
		}

		files.Remove(br.m_path);
		files.Add(br.m_path, br);
		// print("added luau file: " + br.m_path + " package=" + packageKey);
	}

	public void ClearLuauFiles(string packageKey) {
		Debug.Log("ClearLuauFiles: " + packageKey);
		if (this.luauFiles.TryGetValue(packageKey, out var files)) {
			foreach (var br in files.Values) {
				Object.Destroy(br);
			}
			this.luauFiles.Remove(packageKey);
		}
	}

	public static string GetLoadedAssetBundleKey(AirshipPackage package, string assetBundleFile) {
		return package.id + "_" + assetBundleFile;
	}

	private IEnumerator LoadSingleAssetBundleFromAirshipPackage(AirshipPackage airshipPackage, string assetBundleFile, ushort netCollectionId) {
		// ReSharper disable once ReplaceWithSingleAssignment.True
		bool doNetworkPrefabLoading = true;
		// check if client is in the main menu
		if (InstanceFinder.IsOffline && RunCore.IsClient()) {
			doNetworkPrefabLoading = false;
		}

		string assetBundleId = GetLoadedAssetBundleKey(airshipPackage, assetBundleFile);
		if (this.loadedAssetBundles.ContainsKey(assetBundleId)) {
			// Debug.Log($"AssetBundle \"{assetBundleId}\" was already loaded. Skipping load.");
			var existingBundle = this.loadedAssetBundles[assetBundleId];
			if (doNetworkPrefabLoading) {
				existingBundle.netCollectionId = netCollectionId;
				yield return networkNetworkPrefabLoader.LoadNetworkObjects(existingBundle.assetBundle, netCollectionId);
			}
			yield break;
		}

		string bundleFilePath = Path.Join(airshipPackage.GetPersistentDataDirectory(AirshipPlatformUtil.GetLocalPlatform()), assetBundleFile);

		if (!File.Exists(bundleFilePath) || !File.Exists(bundleFilePath + "_downloadSuccess.txt")) {
			// Debug.Log($"Bundle file did not exist \"{bundleFilePath}\". skipping.");
			yield break;
		}

		var st = Stopwatch.StartNew();
		var bundleCreateRequest = AssetBundle.LoadFromFileAsync(bundleFilePath);
		yield return bundleCreateRequest;
		Debug.Log($"Loaded AssetBundle {airshipPackage.id}/{assetBundleFile} from file in {st.ElapsedMilliseconds}ms");

		var assetBundle = bundleCreateRequest.assetBundle;
		if (assetBundle == null) {
			if (!assetBundleFile.Contains("/scenes")) {
				Debug.LogError($"AssetBundle failed to load. name: {airshipPackage.id}/{assetBundleFile}, bundleFilePath: {bundleFilePath}");
			}
			yield break;
		}

#if UNITY_SERVER
		// Debug.Log($"Listing files for {airshipPackage.id}/{assetBundleFile}:");
		// var files = assetBundle.GetAllAssetNames();
		// foreach (var file in files) {
		// 	Debug.Log("	- " + file);
		// }
		// Debug.Log("");
		// Debug.Log($"Listing scenes for {airshipPackage.id}/{assetBundleFile}:");
		// foreach (var scene in assetBundle.GetAllScenePaths()) {
		// 	Debug.Log("  - " + scene);
		// }
		// Debug.Log("");
#endif

		var loadedAssetBundle = new LoadedAssetBundle(airshipPackage, assetBundleFile, assetBundle, netCollectionId);
		loadedAssetBundles.Add(assetBundleId, loadedAssetBundle);

		if (doNetworkPrefabLoading) { // InstanceFinder.IsOffline && RunCore.IsClient()
			yield return networkNetworkPrefabLoader.LoadNetworkObjects(assetBundle, netCollectionId);
		}
	}
}
