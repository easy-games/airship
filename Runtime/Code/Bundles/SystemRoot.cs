using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Code.Bootstrap;
using Luau;
using System;
using Airship.DevConsole;
using Code.Bundles;
using Mirror;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class SystemRoot : Singleton<SystemRoot> {
	public Dictionary<string, LoadedAssetBundle> loadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

	public Dictionary<string, Dictionary<string, AirshipScript>> luauFiles = new();

	private NetworkPrefabLoader networkNetworkPrefabLoader = new NetworkPrefabLoader();
	public ushort networkCollectionIdCounter = 1;

	[FormerlySerializedAs("cacheCodeZip")] public bool codeZipCacheEnabled = false;

	public string currentCoreVersion = "";
	public string currentCoreMaterialsVersion = "";

	public List<AssetBundleCreateRequest> extraBundleLoadRequests = new();
	public static bool startedLoadingExtraBundle = false;
	public static bool preWarmedCoreShaders = false;
	public AssetBundle coreMaterialsAssetBundle;

	private void Awake() {
		DontDestroyOnLoad(this);
		// gameObject.hideFlags = HideFlags.DontSave;
	}

	private void Start() {
		this.RegisterCommands();

#if AIRSHIP_PLAYER
		this.LoadExtraBundle();
#endif
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void OnReload() {
		startedLoadingExtraBundle = false;
		preWarmedCoreShaders = false;
	}

	private async void LoadExtraBundle() {
		if (startedLoadingExtraBundle) return;
		startedLoadingExtraBundle = true;

		// debug: load extra bundles folder
		var extraBundlesDir = Path.Join(Application.persistentDataPath, "ExtraBundles");
		if (Directory.Exists(extraBundlesDir)) {
			string[] bundlePaths = Directory.GetFiles(extraBundlesDir);
			foreach (var path in bundlePaths) {
				var fileName = Path.GetFileName(path);
				// Ignore ".DS_Store"
				if (fileName.StartsWith(".")) continue;

				Debug.Log("Loading extra asset bundle: " + fileName);
				var st = Stopwatch.StartNew();
				startedLoadingExtraBundle = true;

				var ao = AssetBundle.LoadFromFileAsync(path);
				this.extraBundleLoadRequests.Add(ao);
				await ao;
				Debug.Log($"Loaded extra asset bundle {fileName} in {st.ElapsedMilliseconds}ms");
			}
		}

		// CoreMaterials
		{
			var platform = AirshipPlatformUtil.GetLocalPlatform();
			if (platform == AirshipPlatform.Linux) {
				platform = AirshipPlatform.Windows;
			}
			var path = Path.Join(Application.streamingAssetsPath, "ShippedBundles", $"CoreMaterials_{platform}/@easy/corematerials_shared/resources");
#if UNITY_ANDROID
			Debug.Log($"Loading CoreMaterials (Android)... ({path})");
			var st = Stopwatch.StartNew();
			using var req = UnityWebRequestAssetBundle.GetAssetBundle(path);
			await req.SendWebRequest();
			if (req.result != UnityWebRequest.Result.Success) {
				Debug.LogError($"Failed to load core materials bundle: {req.error}");
			} else {
				Debug.Log("Getting asset bundle from request");
				var bundle = DownloadHandlerAssetBundle.GetContent(req);
				if (bundle == null) {
					
				}
				this.coreMaterialsAssetBundle = bundle;
				Debug.Log($"Loaded CoreMaterials bundle in {st.ElapsedMilliseconds} ms.");
			}
#else
			if (File.Exists(path)) {
				Debug.Log($"Loading CoreMaterials... ({path})");
				var st = Stopwatch.StartNew();
				var ao = AssetBundle.LoadFromFileAsync(path);
				this.extraBundleLoadRequests.Add(ao);
				await ao;
				this.coreMaterialsAssetBundle = ao.assetBundle;
				Debug.Log($"Loaded CoreMaterials bundle in {st.ElapsedMilliseconds} ms.");
			} else {
				Debug.LogWarning($"CoreMaterials path not found ({path})");
			}
#endif
		}
	}

	public bool IsUsingBundles() {
#if AIRSHIP_PLAYER
		return true;
#endif
		bool useBundles = true;
		if (Application.isEditor) {
			useBundles = false;
			if (!CrossSceneState.IsLocalServer() && !CrossSceneState.UseLocalBundles) {
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
	public IEnumerator LoadPackages(List<AirshipPackage> packages, bool useUnityAssetBundles, bool forceUnloadAll = true, bool compileLuaOnClient = false, Action<string> onLoadingScreenStep = null) {
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
		var sw = Stopwatch.StartNew();

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

		var enums = new List<IEnumerator>();
		foreach (var bundleId in unloadList) {
			var loadedBundle = this.loadedAssetBundles[bundleId];
			enums.Add(this.UnloadBundleAsync(loadedBundle));
		}
		yield return this.WaitAll(enums.ToArray());

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

						if (entry.Name.EndsWith(".asbuildinfo")) {
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
#if UNITY_SERVER
								// print("Compiled " + entry.FullName + (!airshipBehaviour ? "" : " (AirshipBehaviour)") + " (package: " + package.id + ")");
#endif
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

#if AIRSHIP_PLAYER || true
			try {
				Debug.Log("Scanning network prefabs...");
				Debug.Log($"Listing {NetworkClient.prefabs.Count} network prefabs:");
				int i = 1;
				foreach (var pair in NetworkClient.prefabs) {
					if (pair.Value != null) {
						Debug.Log($"  {i}. {pair.Value.name} ({pair.Key})");
						i++;
					}
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}
#endif

			yield return this.WaitAll(loadLists[0].ToArray());
			
			foreach (var ao in this.extraBundleLoadRequests) {
				if (!ao.isDone) {
					Debug.Log("Waiting for shipped asset bundles to load...");
					yield return ao;
				}
			}

			// Shader Variant Collections
			if (!preWarmedCoreShaders && RunCore.IsClient()) {
#if !UNITY_IOS && !UNITY_ANDROID && !UNITY_EDITOR
				preWarmedCoreShaders = true;
				string[] collections = new[] {
					"MainMenu",
					// "RacingGame",
					// "BWShaderVariants 1 (Raven)",
				};
				int totalCounter = 0;
				foreach (var collectionName in collections) {
					onLoadingScreenStep?.Invoke($"Prewarming shaders (1)");
					var path = $"Assets/AirshipPackages/@Easy/CoreMaterials/ShaderVariantCollections/{collectionName}.shadervariants".ToLower();
					Debug.Log("Loading shader variant: " + collectionName);
					if (this.coreMaterialsAssetBundle == null) {
						Debug.LogWarning("coreMaterialsAssetBundle is null");
					}
					var shaderVariants = this.coreMaterialsAssetBundle.LoadAssetAsync<ShaderVariantCollection>(path);
					Debug.Log($"Loading shader from path: {path}");
					yield return shaderVariants;
					if (shaderVariants != null) {
						var st = Stopwatch.StartNew();
						var col = shaderVariants.asset as ShaderVariantCollection;
						if (col == null) {
							Debug.LogWarning("Unable to warmup missing shader variant collection: " + collectionName);
							continue;
						}

						int variantCounter = 0;
						int step = 1;
						while (!col.WarmUpProgressively(step)) {
							variantCounter += step;
							totalCounter += step;
							onLoadingScreenStep?.Invoke($"Prewarming shaders ({totalCounter + 1})");
							// Debug.Log($"Prewarm in progress ({collectionName}): {variantCounter}");
							yield return null;
						}
						Debug.Log("Prewarmed " + collectionName + " in " + st.ElapsedMilliseconds + "ms");
					}
				}
#endif
			}
		} else {
			if (NetworkClient.isConnected) {
#if UNITY_EDITOR
				var st = Stopwatch.StartNew();
				var guids = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
				Array.Sort(guids);
				foreach (var guid in guids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var networkPrefabCollection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
					foreach (var obj in networkPrefabCollection.networkPrefabs) {
						if (obj == null) continue;
						if (obj is GameObject go) {
							NetworkClient.RegisterPrefab(go);
						}
					}
				}
				#if AIRSHIP_INTERNAL
				Debug.Log($"[Airship]: Registered network prefabs in {st.ElapsedMilliseconds} ms.");
				#endif
#endif
			}
		}

		foreach (var package in packages) {
			if (package.id.ToLower() == "@easy/core") {
				this.currentCoreVersion = package.codeVersion;
			} else if (package.id.ToLower() == "@easy/corematerials") {
				this.currentCoreMaterialsVersion = package.codeVersion;
			}
		}

#if AIRSHIP_PLAYER
		Debug.Log("[Airship]: Finished loading asset bundles in " + sw.ElapsedMilliseconds + " ms.");
#endif
	}

	public IEnumerator UnloadBundleAsync(LoadedAssetBundle loadedBundle) {
		Debug.Log($"[SystemRoot]: Unloading bundle {loadedBundle.bundleId}/{loadedBundle.assetBundleFile}");
		this.networkNetworkPrefabLoader.UnloadNetCollectionId(loadedBundle.netCollectionId);

		if (this.luauFiles.TryGetValue(loadedBundle.bundleId, out var files)) {
			int count = files.Count;
			foreach (var s in files.Values) {
				Object.Destroy(s);
			}
			this.luauFiles.Remove(loadedBundle.bundleId);
			Debug.Log($"Unloaded {count} script files.");
		}

		var ao = loadedBundle.assetBundle.UnloadAsync(true);
		while (!ao.isDone) {
			yield return null;
		}

		loadedBundle.assetBundle = null;
		var key = SystemRoot.GetLoadedAssetBundleKey(loadedBundle.airshipPackage, loadedBundle.assetBundleFile);
		loadedAssetBundles.Remove(key);
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

	public static string GetLoadedAssetBundleKey(AirshipPackage package, string assetBundleFile) {
		return package.id + "_" + assetBundleFile;
	}

	private IEnumerator LoadSingleAssetBundleFromAirshipPackage(AirshipPackage airshipPackage, string assetBundleFile, ushort netCollectionId) {
		// ReSharper disable once ReplaceWithSingleAssignment.True
		bool doNetworkPrefabLoading = true;
		// check if client is in the main menu
		if (!NetworkClient.isConnected && RunCore.IsClient()) {
			doNetworkPrefabLoading = false;
		}

		string assetBundleId = GetLoadedAssetBundleKey(airshipPackage, assetBundleFile);
		if (this.loadedAssetBundles.ContainsKey(assetBundleId)) {
			// Debug.Log($"AssetBundle \"{assetBundleId}\" was already loaded. Skipping load.");
			var existingBundle = this.loadedAssetBundles[assetBundleId];
			if (doNetworkPrefabLoading) {
				existingBundle.netCollectionId = netCollectionId;
				yield return networkNetworkPrefabLoader.RegisterNetworkObjects(existingBundle.assetBundle, netCollectionId);
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
			yield return networkNetworkPrefabLoader.RegisterNetworkObjects(assetBundle, netCollectionId);
		}
	}

	private void RegisterCommands() {
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

		DevConsole.AddCommand(Command.Create("bundles", "", "view loaded asset bundles", () => {
			int i = 1;
			foreach (var pair in this.loadedAssetBundles) {
				Debug.Log($"{i}. {pair.Key}");
				i++;
			}
		}));

		DevConsole.AddCommand(Command.Create("delcode", "", "Clears all code.zip caches", () => {
			var path = Path.Join(Application.persistentDataPath, "Scripts");
			if (Directory.Exists(path)) {
				Directory.Delete(path, true);
			}

			print("Successfully cleared code.zip cache.");
		}));

		DevConsole.AddCommand(Command.Create<bool>(
			"cachecode",
			"",
			"Toggles code.zip caching",
			Parameter.Create("enabled", ""),
			(val) => {
				this.codeZipCacheEnabled = val;
				Debug.Log("Set code.zip caching enabled to: " + this.codeZipCacheEnabled);
			}, () => {
				this.codeZipCacheEnabled = !this.codeZipCacheEnabled;
				Debug.Log("Set code.zip caching enabled to: " + this.codeZipCacheEnabled);
			}
		));

		DevConsole.AddCommand(Command.Create("trackedidentities", "", "Network Identity Leaks", () => {
			var count = 0;
			foreach (var entry in ThreadDataManager.s_reverseObjectKeys)
			{
				if (entry.Key is NetworkIdentity) {
					count++;
				}				
			}
			Debug.Log("Total tracked network identities: " + count);
		}));

		DevConsole.AddCommand(Command.Create<string>(
			"gc",
			"",
			"Luau GC",
			Parameter.Create("state", "Options: full, step, off"),
			(val) => {
				val = val.ToLower();
				switch (val) {
					case "full":
						LuauPlugin.LuauSetGCState(LuauPlugin.LuauGCState.Full);
						Debug.Log("Luau per-frame GC set to FULL");
						break;
					case "step":
						LuauPlugin.LuauSetGCState(LuauPlugin.LuauGCState.Step);
						Debug.Log("Luau per-frame GC set to STEP");
						break;
					case "off":
						LuauPlugin.LuauSetGCState(LuauPlugin.LuauGCState.Off);
						Debug.Log("Luau per-frame GC set to OFF");
						break;
					default:
						Debug.Log($"Invalid Luau GC state: \"{val}\"");
						break;
				}
			},
			() => {
				var gcKbGame = LuauPlugin.LuauCountGC(LuauContext.Game);
				var gcKbProtected = LuauPlugin.LuauCountGC(LuauContext.Protected);
				var gcKb = gcKbGame + gcKbProtected;
				Debug.Log($"Luau GC: [Game: {gcKbGame} KB] [Protected: {gcKbProtected} KB] [Total: {gcKb} KB]");
			}
		));

		DevConsole.AddCommand(Command.Create("delpackages", "", "Clears all package caches", () => {
			var path = Path.Join(Application.persistentDataPath, "Packages");
			if (Directory.Exists(path)) {
				Directory.Delete(path, true);
			}

			print("Successfully deleted package cache.");
		}));

		DevConsole.AddCommand(Command.Create("luau", "", "Prints info about the Luau plugin", () => {
			var pluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
			var bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
			var server = FindAnyObjectByType<AirshipLuauDebugger>();
			
			Debug.Log($"CLIENT: {pluginVersion} - Bytecode Version: {bytecodeVersion.Target} (Min: {bytecodeVersion.Min}, Max: {bytecodeVersion.Max})");
			if (server != null) {
				Debug.Log($"SERVER: {server.luauPluginVersion} - Bytecode Version: {server.bytecodeVersion.Target} (Min: {server.bytecodeVersion.Min}, Max: {server.bytecodeVersion.Max})");
				if (pluginVersion != server.luauPluginVersion) {
					Debug.LogWarning("Luau plugin version mismatch between server and client");
				}
			}
		}));

		DevConsole.AddCommand(Command.Create<string>("luauregistry", "", "Prints info about the Luau plugin", Parameter.Create("context", "Options: game, protected"), (val) => {
			val = val.ToLower();
			
			string registryDump;
			switch (val) {
				case "game":
					registryDump = LuauPlugin.LuauDebugCountAllRegistryItems(LuauContext.Game);
					break;
				case "protected":
					registryDump = LuauPlugin.LuauDebugCountAllRegistryItems(LuauContext.Protected);
					break;
				default:
					Debug.Log($"Invalid context: \"{val}\"");
					return;
			}
			
			Debug.Log(registryDump);
		}));

		DevConsole.AddCommand(Command.Create<string>("luauobjects", "", "Prints count of Unity objects tracked by Luau plugin", Parameter.Create("context", "Options: game, protected, game_server, protected_server"), (val) => {
			val = val.ToLower();

			var context = LuauContext.Game;
			var onServer = val.EndsWith("_server", StringComparison.OrdinalIgnoreCase);
			
			int[] instanceIds;
			switch (val) {
				case "game" or "game_server":
					context = LuauContext.Game;
					break;
				case "protected" or "protected_server":
					context = LuauContext.Protected;
					break;
				default:
					Debug.Log($"Invalid context: \"{val}\"");
					return;
			}

			if (onServer) {
				var server = FindAnyObjectByType<AirshipLuauDebugger>();
				server.FetchServerLuauInstanceIds(context);
			} else {
				var str = AirshipLuauDebugger.FetchLuauUnityInstanceIds(context);
				Debug.Log(str);
			}
		}));

		DevConsole.AddCommand(Command.Create("version", "", "Prints version git hash", () => {
			var localVersion = AirshipVersion.GetVersionHash();
			Debug.Log("Client: #" + localVersion);

			var server = FindAnyObjectByType<AirshipLuauDebugger>();
			if (server != null) {
				Debug.Log("Server: #" + server.serverPlayerVersion);
			}
		}));
	}
}
