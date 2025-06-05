using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Code.Bootstrap;
using Editor.Packages;
using Luau;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class CreateAssetBundles {
	public static bool buildingBundles = false;
	public const BuildAssetBundleOptions BUILD_OPTIONS = BuildAssetBundleOptions.ChunkBasedCompression;

	public static bool PrePublishChecks() {
		var terrains = GameObject.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (var terrain in terrains) {
			if (terrain.drawInstanced) {
				Debug.LogError("Terrain with DrawInstancing found in scene " + terrain.gameObject.scene.name + ". DrawInstancing must be disabled.");
				return false;
			}
		}
		return true;
	}

	// [MenuItem("Airship/Tag Asset Bundles")]
	public static bool FixBundleNames() {
		// Set NetworkObject GUIDs
		// var networkPrefabGUIDS = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
		// foreach (var npGuid in networkPrefabGUIDS) {
		// 	var path = AssetDatabase.GUIDToAssetPath(npGuid);
		// 	var prefabCollection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
		// 	foreach (var prefab in prefabCollection.networkPrefabs) {
		// 		if (prefab is GameObject) {
		// 			var go = (GameObject) prefab;
		// 			var nob = go.GetComponent<NetworkObject>();
		// 			if (nob == null) {
		// 				Debug.LogError($"GameObject {go.name} in {path} was missing a NetworkObject.");
		// 				continue;
		// 			}
		//
		// 			nob.airshipGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(nob.gameObject)).ToString();
		// 			EditorUtility.SetDirty(nob);
		// 		}
		// 	}
		// }
		// AssetDatabase.SaveAssets();

		// foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames()) {
		// 	var paths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
		// 	foreach (var path in paths) {
		// 		var importer = AssetImporter.GetAtPath(path);
		// 		importer.assetBundleName = null;
		// 	}
		// }

		return true;

		string[] bundleFiles = new[] {
			// "client/resources",
			// "client/scenes",
			// "server/resources",
			// "server/scenes",
			"shared/resources",
			"shared/scenes"
		};
		// Game Folders
		// foreach (var assetBundleFile in bundleFiles) {
		// 	var isSceneBundle = assetBundleFile.Contains("scenes");
		//
		// 	string folderPath = "assets";
		// 	if (!isSceneBundle) {
		// 		folderPath = "assets/resources";
		// 		var assetImporter = AssetImporter.GetAtPath(folderPath);
		// 		if (assetImporter == null) {
		// 			Debug.LogWarning("No Assets/Resources folder found. Only code and assets in your scenes will be included in your deploy.");
		// 			continue;
		// 		}
		//
		// 		assetImporter.assetBundleName = assetBundleFile;
		// 	} else { // isSceneBundle == true
		// 		folderPath = "assets/scenes";
		// 	}
		//
		// 	var filter = "*";
		// 	if (isSceneBundle) filter = "t:Scene";
		//
		// 	string[] children = AssetDatabase.FindAssets(filter, new []{ folderPath });
		//
		// 	foreach (string childGuid in children) {
		// 		var path = AssetDatabase.GUIDToAssetPath(childGuid);
		// 		var childAssetImporter = AssetImporter.GetAtPath(path);
		// 		childAssetImporter.assetBundleName = $"{assetBundleFile}";
		//
		// 		if (isSceneBundle) {
		// 			if (path.StartsWith("assets/airshippackages")) continue;
		// 		} else {
		// 			if (path.EndsWith(".ts") || path.EndsWith(".d.ts")) continue;
		// 		}
		//
		// 		// Find lighting data.
		// 		if (isSceneBundle) {
		// 			var sceneLightingFolderPath = path.Replace(".unity", "");
		// 			if (!AssetDatabase.AssetPathExists(sceneLightingFolderPath)) continue;
		// 			var lightingChildren = AssetDatabase.FindAssets("*", new[] { sceneLightingFolderPath });
		// 			foreach (string lightingAssetGuid in lightingChildren) {
		// 				var lightingAssetPath = AssetDatabase.GUIDToAssetPath(lightingAssetGuid);
		// 				var lightingAssetImporter = AssetImporter.GetAtPath(lightingAssetPath);
		// 				if (lightingAssetPath.EndsWith("comp_shadowmask.png")) {
		// 					lightingAssetImporter.assetBundleName = null;
		// 				} else {
		// 					lightingAssetImporter.assetBundleName = "shared/resources";
		// 				}
		// 			}
		// 		}
		// 	}
		// }

		// Package folders
		string[] importFolders = AssetDatabase.GetSubFolders("assets/airshippackages");
		foreach (var importFolder in importFolders) {
			if (!importFolder.Contains("@")) continue;

			string[] innerFolders = AssetDatabase.GetSubFolders(importFolder);
			foreach (var packageFolder in innerFolders) {
				var split = packageFolder.Split("/");
				string packageId = split[split.Length - 2] + "/" + split[split.Length - 1];
				var assetImporter = AssetImporter.GetAtPath(packageFolder);
				if (!assetImporter.assetPath.Contains(".unity")) {
					assetImporter.assetBundleName = $"{packageId}_shared/resources";
				}

				foreach (var bundleFile in bundleFiles) {
					var isSceneBundle = bundleFile.Contains("scenes");

					string[] children;
					if (isSceneBundle) {
						children = AssetDatabase.FindAssets("t:Scene", new[] { packageFolder });
					} else {
						children = AssetDatabase
							.FindAssets("*", new[] { packageFolder })
							.ToArray();
					}

					foreach (string childGuid in children) {
						var path = AssetDatabase.GUIDToAssetPath(childGuid);
						if (!isSceneBundle) {
							if (path.EndsWith(".unity") || path.Contains("/Editor/") || path.EndsWith(".ts") || path.EndsWith(".d.ts")) {
								continue;
							}
						}
						var childAssetImporter = AssetImporter.GetAtPath(path);
						childAssetImporter.assetBundleName = $"{packageId}_{bundleFile}";
					}

				}
			}
		}

		return true;
	}

	/// <summary>
	/// Creates an AssetBundleBuild for every AirshipPackage in the project.
	/// </summary>
	/// <returns></returns>
	public static List<AssetBundleBuild> GetPackageAssetBundleBuilds() {
		List<AssetBundleBuild> builds = new();

		if (!Directory.Exists(Path.Join("Assets", "AirshipPackages"))) {
			throw new Exception("Missing \"Assets/AirshipPackages\" folder.");
		}

		var orgDirs = Directory.GetDirectories(Path.Join("Assets", "AirshipPackages"), "*", SearchOption.TopDirectoryOnly);
		foreach (var orgDir in orgDirs) {
			var packageDirs = Directory.GetDirectories(orgDir);
			var orgName = Path.GetFileName(orgDir);
			foreach (var packageDir in packageDirs) {
				var packageName = Path.GetFileName(packageDir);
				var assetBundleName = $"{orgName}/{packageName}_shared/resources".ToLower();
				Debug.Log("asset bundle name: " + assetBundleName);
				var assetGuids = AssetDatabase.FindAssets("*", new string[] { packageDir }).ToList();

				if (assetBundleName == "@easy/corematerials_shared/resources") {
					var addUrpFiles = new Action<string>((string path) => {
						var urpGuids = AssetDatabase.FindAssets("*",
							new string[] { path });
						assetGuids.AddRange(urpGuids);
					});

					if (!EditorIntegrationsConfig.instance.selfCompileAllShaders) {
						Debug.Log("Adding URP assets to CoreMaterials bundle.");
						addUrpFiles("Packages/com.unity.render-pipelines.universal/Shaders");
						addUrpFiles("Packages/com.unity.render-pipelines.universal/ShaderLibrary");
						addUrpFiles("Packages/com.unity.render-pipelines.universal/Textures");
					}
				}

				var assetPaths = assetGuids.Select((guid) => {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					return path;
				})
					.Where((p) => !AssetDatabase.IsValidFolder(p))
					.Where((p) => !p.EndsWith(".unity"))
					.Where((p) => !p.EndsWith(".cs"))
					.Where((p) => !p.EndsWith(".d.ts"))
					.Where((p) => !p.Contains("Packages/com.unity.render-pipelines.universal/Editor"))
					.ToArray();
				var addressableNames = assetPaths.Select((p) => p.ToLower())
					.ToArray();

				var build = new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = assetPaths.ToArray(),
					addressableNames = addressableNames
				};
				builds.Add(build);
			}
		}

		return builds;
	}

#if UNITY_EDITOR
    [MenuItem("Airship/Misc/Test Build Game Config")]
	public static void TestBuildGameConfig(){
		BuildGameConfig();
	}
#endif

	public static GameConfig BuildGameConfig() {
		GameConfig gameConfig = GameConfig.Load();

		// Update tags
		var tagList = UnityEditorInternal.InternalEditorUtility.tags[7..];
		if (tagList.Length > GameConfig.MaximumTags) {
			Debug.LogError($"Maximum number of allowed unity tags in Airship is {GameConfig.MaximumTags} - you have {tagList.Length} defined.");
			return null;
		}
		gameConfig.gameTags = tagList.ToArray();

		// Update layers
		var layers = new List<string>();
		for (int i = 0; i < 31; i++) {
			var layerName = LayerMask.LayerToName(i);
			layers.Add(layerName);
		}
		gameConfig.gameLayers = layers.ToArray();
		gameConfig.SerializeSettings();       
		
		// Local source packages set to forceLatest
		foreach (var package in gameConfig.packages) {
			if (package.localSource) {
				package.forceLatestVersion = true;
			}
		}
		
		EditorUtility.SetDirty(gameConfig);
		AssetDatabase.SaveAssetIfDirty(gameConfig);
		return gameConfig;
	}

	public static bool BuildGameAssetBundles(AirshipPlatform platform, bool useCache = true) {
		ResetScenes();

		if (!PrePublishChecks()) {
			return false;
		}

		if (!FixBundleNames()) {
			Debug.LogError("Failed to tag asset bundles.");
			return false;
		}

		var sw = Stopwatch.StartNew();
		var gameConfig = GameConfig.Load();
		if(!gameConfig){
			return false;
		}

		var buildPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", platform.ToString());
		if (!Directory.Exists(buildPath)) {
			Directory.CreateDirectory(buildPath);
		}
		Debug.Log($"[Editor]: Building {platform} asset bundles...");
		Debug.Log("[Editor]: Build path: " + buildPath);

		List<AssetBundleBuild> builds = GetPackageAssetBundleBuilds();

		// Make a fake asset bundle with all package content. This makes the build have the correct dependency data.
		// {
		// 	var assetGuids = AssetDatabase.FindAssets("*", new string[] { $"Assets/AirshipPackages" }).ToList();
		// 	var assetPaths = assetGuids.Select((guid) => {
		// 		var path = AssetDatabase.GUIDToAssetPath(guid);
		// 		return path;
		// 	}).Where((p) => !AssetDatabase.IsValidFolder(p)).ToArray();
		// 	var addressableNames = assetPaths.Select((p) => p.ToLower())
		// 		.ToArray();
		// 	var build = new AssetBundleBuild() {
		// 		assetBundleName = "fake_packages",
		// 		assetNames = assetPaths.ToArray(),
		// 		addressableNames = addressableNames
		// 	};
		// 	builds.Add(build);
		// }

		foreach (var assetBundleFile in AirshipPackagesWindow.assetBundleFiles) {
			var assetBundleName = assetBundleFile.ToLower();
			if (assetBundleName == "shared/scenes") {
				var assetGuids = gameConfig.gameScenes
					.Select((s) => AssetDatabase.GetAssetPath((SceneAsset)s)).ToHashSet();

				var explicitlyAddedPaths = AssetDatabase.GetAssetPathsFromAssetBundle("scenes");
				Debug.Log($"Found {explicitlyAddedPaths.Length} explicit assets for scenes bundle.");
				foreach (var path in explicitlyAddedPaths) {
					// Debug.Log("  - " + path);
					assetGuids.Add(AssetDatabase.AssetPathToGUID(path));
				}

				string[] assetPaths = assetGuids
					.Where((path) => !(path.EndsWith(".lua") || path.EndsWith(".json~")))
					.ToArray();
				Debug.Log("Including assets in scenes bundle:");
				foreach (var p in assetPaths) {
					Debug.Log("  - " + p);
				}

				var addressableNames = assetPaths.Select((p) => p.ToLower())
					.ToArray();
				var build = new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = assetPaths,
					addressableNames = addressableNames
				};
				builds.Add(build);
			} else {
				if (assetBundleName != "shared/resources") continue;

				var assetGuids = AssetDatabase.FindAssets("*", new string[] {"Assets/Resources"}).ToHashSet();
				if (AssetDatabase.AssetPathExists("Assets/Airship.asbuildinfo")) {
					assetGuids.Add(AssetDatabase.AssetPathToGUID("Assets/Airship.asbuildinfo"));
				}
				if (AssetDatabase.AssetPathExists("Assets/GameConfig.asset")) {
					assetGuids.Add(AssetDatabase.AssetPathToGUID("Assets/GameConfig.asset"));
				}
				if (AssetDatabase.AssetPathExists("Assets/NetworkPrefabCollection.asset")) {
					assetGuids.Add(AssetDatabase.AssetPathToGUID("Assets/NetworkPrefabCollection.asset"));
				}

				var explicitlyAddedPaths = AssetDatabase.GetAssetPathsFromAssetBundle("resources");
				Debug.Log($"Found {explicitlyAddedPaths.Length} explicit assets for resources bundle.");
				foreach (var path in explicitlyAddedPaths) {
					// Debug.Log("  - " + path);
					assetGuids.Add(AssetDatabase.AssetPathToGUID(path));
				}

				var assetPaths = assetGuids
					.Select((guid) => AssetDatabase.GUIDToAssetPath(guid))
					.Where((p) => !(p.EndsWith(".lua") || p.EndsWith(".json~") || p.EndsWith(".d.ts")))
					.Where((path) => !path.ToLower().Contains("editor/"))
					.Where((p) => !AssetDatabase.IsValidFolder(p))
					.ToArray();
				// Debug.Log("Resources:");
				// foreach (var path in assetPaths) {
				// 	Debug.Log("  - " + path);
				// }
				var addressableNames = assetPaths
					.Select((p) => p.ToLower())
					.ToArray();
				builds.Add(new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = assetPaths,
					addressableNames = addressableNames
				});
			}
		}

		// var tasks = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleBuiltInShaderExtraction);
		var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);

		if (platform == AirshipPlatform.Android) {
			PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
			PlayerSettings.SetGraphicsAPIs(buildTarget, new GraphicsDeviceType[]
			{
				GraphicsDeviceType.Vulkan
			});
		} else {
			PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, true);
		}

		var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
		if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
			buildTargetGroup = BuildTargetGroup.Standalone;
		}
		EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
		var buildParams = new BundleBuildParameters(
			buildTarget,
			buildTargetGroup,
			buildPath
		) {
			WriteLinkXML = false,
		};
		buildParams.UseCache = useCache;
		EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
		buildParams.BundleCompression = BuildCompression.LZ4;
		var buildContent = new BundleBuildContent(builds);

		// Debug.Log("Additional files:");
		// foreach (var pair in buildContent.AdditionalFiles) {
		// 	Debug.Log(pair.Key + ":");
		// 	foreach (var p in pair.Value) {
		// 		Debug.Log("  - " + p.fileAlias);
		// 	}
		// }

		ContentPipeline.BuildCallbacks.PostPackingCallback = (parameters, data, arg3) => {
			return ReturnCode.Success;
		};

		AirshipPackagesWindow.buildingPackageId = "game";
		buildingBundles = true;
		AirshipScriptableBuildPipelineConfig.buildingGameBundles = true;
		ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result);
		buildingBundles = false;
		AirshipScriptableBuildPipelineConfig.buildingGameBundles = false;
		if (returnCode != ReturnCode.Success) {
			Debug.LogError("Failed to build asset bundles. ReturnCode=" + returnCode);
			return false;
		}

		// Debug.Log("----------------------");
		// Debug.Log("Airship Build Report");
		// Debug.Log("----------------------");
		// {
		// 	Debug.Log("Serialized Files:");
		// 	foreach (var pair in result.WriteResults) {
		// 		Debug.Log("  " + pair.Key + ":");
		// 		for (int i = 0; i < pair.Value.serializedObjects.Count; i++) {
		// 			var objects = pair.Value.serializedObjects[i];
		// 			var sizeKb = objects.rawData.size / 1000;
		// 			Debug.Log($"    {i}. ({sizeKb:n0} kb) {AssetDatabase.GUIDToAssetPath(objects.serializedObject.guid)}");
		// 		}
		// 	}
		// }
		// {
		// 	foreach (var pair in result.BundleInfos) {
		// 		Debug.Log($"{pair.Key} Dependencies:");
		// 		for (int i = 0; i < pair.Value.Dependencies.Length; i++) {
		// 			Debug.Log($"  {i}. {pair.Value.Dependencies[i]}");
		// 		}
		// 	}
		// }
		// {
		// 	Debug.Log("Asset results:");
		// 	foreach (var pair in result.AssetResults) {
		// 		Debug.Log("  " + AssetDatabase.GUIDToAssetPath(pair.Key) + ":");
		// 		for (int i = 0; i < pair.Value.IncludedObjects.Count; i++) {
		// 			var includedObject = pair.Value.IncludedObjects[i];
		// 			Debug.Log($"    {i}. {AssetDatabase.GUIDToAssetPath(includedObject.guid)}");
		// 		}
		// 		Debug.Log("  Referenced Objects:");
		// 		if (pair.Value.ReferencedObjects != null) {
		// 			for (int i = 0; i < pair.Value.ReferencedObjects.Count; i++) {
		// 				var referencedObject = pair.Value.ReferencedObjects[i];
		// 				Debug.Log($"    {i}. (dep) {AssetDatabase.GUIDToAssetPath(referencedObject.guid)}");
		// 			}
		// 		}
		//
		// 	}
		// }

		Debug.Log($"[Editor]: Finished building {platform} asset bundles in {sw.Elapsed.TotalSeconds} seconds.");

		return true;
	}

	static IList<IBuildTask> GetBuildTasks()
	{
		var buildTasks = new List<IBuildTask>();

		// Setup
		buildTasks.Add(new SwitchToBuildPlatform());
		buildTasks.Add(new RebuildSpriteAtlasCache());

		// Player Scripts
		buildTasks.Add(new BuildPlayerScripts());
		buildTasks.Add(new PostScriptsCallback());

		// Dependency
		buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
		buildTasks.Add(new CalculateCustomDependencyData());
#endif
		buildTasks.Add(new CalculateAssetDependencyData());
		buildTasks.Add(new StripUnusedSpriteSources());
		// if (shaderTask)
		// 	buildTasks.Add(new CreateBuiltInBundle("UnityBuiltIn.bundle"));
		// if (monoscriptTask)
		// 	buildTasks.Add(new CreateMonoScriptBundle("UnityMonoScripts.bundle"));
		buildTasks.Add(new PostDependencyCallback());

		// Packing
		buildTasks.Add(new GenerateBundlePacking());
		// if (shaderTask || monoscriptTask)
		// 	buildTasks.Add(new UpdateBundleObjectLayout());
		buildTasks.Add(new GenerateBundleCommands());
		buildTasks.Add(new GenerateSubAssetPathMaps());
		buildTasks.Add(new GenerateBundleMaps());
		buildTasks.Add(new PostPackingCallback());

		// Writing
		buildTasks.Add(new WriteSerializedFiles());
		buildTasks.Add(new ArchiveAndCompressBundles());
		buildTasks.Add(new AppendBundleHash());
		buildTasks.Add(new GenerateLinkXml());
		buildTasks.Add(new PostWritingCallback());

		return buildTasks;
	}

	public static void BuildLocalAssetBundles()
	{
		BuildGameAssetBundles(AirshipPlatformUtil.GetLocalPlatform());
	}

#if AIRSHIP_INTERNAL
	[MenuItem("Airship/Internal/Build iOS Game Bundles")]
	public static void BuildiOSAssetBundles() {
		BuildGameAssetBundles(AirshipPlatform.iOS);
	}
	
	[MenuItem("Airship/Internal/Build Android Game Bundles")]
	public static void BuildAndroidAssetBundles() {
		BuildGameAssetBundles(AirshipPlatform.Android);
	}
#endif

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Build Local AssetBundles")]
#endif
	public static void BuildLocalAssetBundlesMenuItem() {
		var platform = AirshipPlatformUtil.FromRuntimePlatform(Application.platform);
		BuildPlatforms(new[] {platform});
	}

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Delete Local AssetBundles")]
#endif
	public static void DeleteLocalAssetBundles()
	{
		Debug.Log("Deleting local asset bundles in " + AssetBridge.GamesPath);
		if (Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.Delete(AssetBridge.GamesPath, true);
		}

		Debug.Log("Finished deleting local asset bundles!");
	}

	// [MenuItem("Airship/Custom Local Bundle/Linux")]
	public static void BuildLinuxPlayerAssetBundlesAsLocal()
	{
		BuildGameAssetBundles(AirshipPlatform.Linux);
	}

	// [MenuItem("Airship/Custom Local Bundle/Windows")]
	public static void BuildWindowsPlayerAssetBundlesAsLocal()
	{
		BuildGameAssetBundles(AirshipPlatform.Windows);
	}

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Build All AssetBundles")]
#endif
	public static void BuildAllAssetBundles() {
		BuildPlatforms(AirshipPlatformUtil.livePlatforms);
	}

	public static bool BuildPlatforms(AirshipPlatform[] platforms, bool useCache = true) {
		var sw = Stopwatch.StartNew();
		try {
			foreach (var platform in platforms) {
				var res = BuildGameAssetBundles(platform, useCache);
				if (!res) {
					return false;
				}
			}

			Debug.Log($"Built game asset bundles for {platforms.Length} platform{(platforms.Length > 1 ? "s" : "")} in {sw.Elapsed.TotalSeconds.ToString("0.0")}s");
		} catch (Exception e) {
			Debug.LogException(e);
			Debug.LogError($"Failed to build asset bundles.");
			return false;
		}

		AddAllGameBundleScenes();
		return true;
	}

	public static void AddAllGameBundleScenes()
	{
#if UNITY_EDITOR
		var config = FindGameConfig();

		List<EditorBuildSettingsScene> list = new();
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/Login.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/Disconnected.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/AirshipUpdateApp.unity", true));

		if (config != null)
		{
			foreach (var s in config.gameScenes)
			{
				string pathToScene = AssetDatabase.GetAssetPath(s);
				list.Add(new EditorBuildSettingsScene(pathToScene, true));
			}
		}

		EditorBuildSettings.scenes = list.ToArray();
#endif
	}

	public static void ResetScenes()
	{
#if UNITY_EDITOR && AIRSHIP_PLAYER
		EditorBuildSettingsScene[] scenes = new[]
		{
			new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true),
			new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true),
		};
		EditorBuildSettings.scenes = scenes;
#endif
	}

	public static GameConfig FindGameConfig()
	{
		return AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
	}
}

// ensure class initializer is called whenever scripts recompile
[InitializeOnLoad]
public static class PlayModeStateChangedExample
{
	// register an event handler when the class is initialized
	static PlayModeStateChangedExample()
	{
		EditorApplication.playModeStateChanged += LogPlayModeState;
		CreateAssetBundles.ResetScenes();
	}

	private static void LogPlayModeState(PlayModeStateChange state) {
		//Debug.Log(state);
		if (state == PlayModeStateChange.ExitingEditMode) {
			CreateAssetBundles.AddAllGameBundleScenes();
			if (SceneManager.GetActiveScene().name != "CoreScene") {
				return;
			}

			// Debug.Log("[EDITOR]: Building asset bundles..");
			// CreateAssetBundles.BuildLocalAssetBundles();
		} else if (state == PlayModeStateChange.EnteredEditMode) {
			CreateAssetBundles.ResetScenes();
		}
	}
}