using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Code.Bootstrap;
using Editor.Packages;
using FishNet.Object;
using UnityEditor.Build.Pipeline;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class CreateAssetBundles {
	public static bool buildingBundles = false;
	public const BuildAssetBundleOptions BUILD_OPTIONS = BuildAssetBundleOptions.ChunkBasedCompression;

	[MenuItem("Airship/Tag Asset Bundles")]
	public static bool FixBundleNames() {
		foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames()) {
			var paths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
			foreach (var path in paths) {
				var importer = AssetImporter.GetAtPath(path);
				importer.assetBundleName = null;
			}
		}

		string[] bundleFiles = new[] {
			// "client/resources",
			// "client/scenes",
			// "server/resources",
			// "server/scenes",
			"shared/resources",
			"shared/scenes"
		};
		// Game Folders
		foreach (var assetBundleFile in bundleFiles) {
			var isSceneBundle = assetBundleFile.Contains("scenes");

			string folderPath = "assets";
			if (!isSceneBundle) {
				folderPath = "assets/resources";
				var assetImporter = AssetImporter.GetAtPath(folderPath);
				assetImporter.assetBundleName = assetBundleFile;
			}

			var filter = "*";
			if (isSceneBundle) filter = "t:Scene";

			string[] children = AssetDatabase.FindAssets(filter, new []{ folderPath });

			foreach (string childGuid in children) {
				var path = AssetDatabase.GUIDToAssetPath(childGuid);
				var childAssetImporter = AssetImporter.GetAtPath(path);
				childAssetImporter.assetBundleName = $"{assetBundleFile}";

				if (isSceneBundle) {
					if (path.StartsWith("assets/airshippackages")) continue;
				} else {
					if (path.EndsWith(".ts") || path.EndsWith(".d.ts")) continue;
				}

				// Find lighting data.
				if (isSceneBundle) {
					var sceneLightingFolderPath = path.Replace(".unity", "");
					if (!AssetDatabase.AssetPathExists(sceneLightingFolderPath)) continue;
					var lightingChildren = AssetDatabase.FindAssets("*", new[] { sceneLightingFolderPath });
					foreach (string lightingAssetGuid in lightingChildren) {
						var lightingAssetPath = AssetDatabase.GUIDToAssetPath(lightingAssetGuid);
						var lightingAssetImporter = AssetImporter.GetAtPath(lightingAssetPath);
						if (lightingAssetPath.EndsWith("comp_shadowmask.png")) {
							lightingAssetImporter.assetBundleName = null;
						} else {
							lightingAssetImporter.assetBundleName = "shared/resources";
						}
					}
				}
			}
		}

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

		// Set NetworkObject GUIDs
		var networkPrefabGUIDS = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
		foreach (var npGuid in networkPrefabGUIDS) {
			var path = AssetDatabase.GUIDToAssetPath(npGuid);
			var prefabCollection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
			foreach (var prefab in prefabCollection.networkPrefabs) {
				if (prefab is GameObject) {
					var go = (GameObject) prefab;
					var nob = go.GetComponent<NetworkObject>();
					if (nob == null) {
						Debug.LogError($"GameObject {go.name} in {path} was missing a NetworkObject.");
						continue;
					}

					nob.airshipGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(nob.gameObject)).ToString();
				}
			}
		}

		return true;
	}

	private static bool BuildGameAssetBundles(AirshipPlatform platform, bool useCache = true) {
		ResetScenes();
		if (!FixBundleNames()) {
			Debug.LogError("Failed to tag asset bundles.");
			return false;
		}

		var sw = Stopwatch.StartNew();
		var gameConfig = GameConfig.Load();
		var buildPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", platform.ToString());
		if (!Directory.Exists(buildPath)) {
			Directory.CreateDirectory(buildPath);
		}
		Debug.Log($"[Editor]: Building {platform} asset bundles...");
		Debug.Log("[Editor]: Build path: " + buildPath);

		List<AssetBundleBuild> builds = new();
		foreach (var assetBundleFile in AirshipPackagesWindow.assetBundleFiles) {
			var assetBundleName = assetBundleFile.ToLower();
			if (assetBundleName == "shared/scenes") {
				string[] assetPaths = gameConfig.gameScenes.Select((s) => AssetDatabase.GetAssetPath((SceneAsset)s)).ToArray();
				Debug.Log("Including scenes: ");
				foreach (var p in assetPaths) {
					Debug.Log("  - " + p);
				}
				var addressableNames = assetPaths.Select((p) => p.ToLower())
					// .Where((p) => !(p.EndsWith(".lua") || p.EndsWith(".json~")))
					.ToArray();
				builds.Add(new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = assetPaths,
					addressableNames = addressableNames
				});
			} else {
				string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)
					// .Where((path) => !(path.EndsWith(".lua") || path.EndsWith(".json~")))
					.ToArray();
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
		var tasks = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleBuiltInShaderExtraction);
		var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);
		var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
		if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
			buildTargetGroup = BuildTargetGroup.Standalone;
		}
		var buildParams = new BundleBuildParameters(
			buildTarget,
			buildTargetGroup,
			buildPath
		);
		buildParams.UseCache = useCache;
		buildParams.BundleCompression = BuildCompression.LZ4;
		EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
		var buildContent = new BundleBuildContent(builds);
		AirshipPackagesWindow.buildingPackageId = "game";
		buildingBundles = true;
		ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result, tasks);
		buildingBundles = false;
		if (returnCode != ReturnCode.Success) {
			Debug.LogError("Failed to build asset bundles. ReturnCode=" + returnCode);
			return false;
		}

		Debug.Log($"[Editor]: Finished building {platform} asset bundles in {sw.Elapsed.TotalSeconds} seconds.");
		return true;
	}

	public static void BuildLocalAssetBundles()
	{
		BuildGameAssetBundles(AirshipPlatformUtil.GetLocalPlatform());
	}

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
		try
		{
			foreach (var platform in platforms) {
				var res = BuildGameAssetBundles(platform, useCache);
				if (!res) {
					return false;
				}
			}

			Debug.Log($"Rebuilt game asset bundles for {platforms.Length} platform{(platforms.Length > 1 ? "s" : "")} in {sw.Elapsed.TotalSeconds}s");
		}
		catch (Exception e)
		{
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

	private static void LogPlayModeState(PlayModeStateChange state)
	{
		//Debug.Log(state);
		if (state == PlayModeStateChange.ExitingEditMode)
		{
			CreateAssetBundles.AddAllGameBundleScenes();
			if (SceneManager.GetActiveScene().name != "CoreScene")
			{
				return;
			}

			var config = AirshipEditorConfig.Load();
			if (!config.buildBundlesOnPlay) return;

			Debug.Log("[EDITOR]: Building asset bundles..");
			CreateAssetBundles.BuildLocalAssetBundles();
		}
		else if (state == PlayModeStateChange.EnteredEditMode)
		{
			CreateAssetBundles.ResetScenes();
		}
	}
}