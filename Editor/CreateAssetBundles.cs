using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Code.Bootstrap;
using Editor.Packages;
using UnityEditor.Build.Pipeline;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class CreateAssetBundles {
	public const BuildAssetBundleOptions BUILD_OPTIONS = BuildAssetBundleOptions.ChunkBasedCompression;

	public static void FixBundleNames()
	{
		string[] gameBundles = new[] {
			"client/resources",
			"client/scenes",
			"server/resources",
			"server/scenes",
			"shared/resources",
			"shared/scenes"
		};
		foreach (var assetBundleFile in gameBundles) {
			var assetImporter = AssetImporter.GetAtPath(Path.Combine(BootstrapHelper.GameBundleRelativeRootPath, assetBundleFile));
			assetImporter.assetBundleName = assetBundleFile;
		}

		string[] importFolders = AssetDatabase.GetSubFolders(BootstrapHelper.ImportsBundleRelativeRootPath);
		foreach (var importFolder in importFolders) {
			Debug.Log("import folder: " + importFolder);
			var split = importFolder.Split(Path.DirectorySeparatorChar);
			var importFolderName = split[split.Length - 1];
			foreach (var bundle in gameBundles) {
				var bundlePath = Path.Join(importFolder, bundle);
				if (!Directory.Exists(bundlePath)) {
					throw new Exception($"Package folder \"{importFolderName}/{bundle}\" was missing. Please create it. Folder path: {bundlePath}");
				}
				var assetImporter = AssetImporter.GetAtPath(bundlePath);
				assetImporter.assetBundleName =  $"{importFolderName}_{bundle}";
			}
		}
	}

	private static void BuildGameAssetBundles(AirshipPlatform platform) {
		ResetScenes();
		FixBundleNames();

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
			var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
			var addressableNames = assetPaths.Select((p) => p.ToLower()).ToArray();
			builds.Add(new AssetBundleBuild() {
				assetBundleName = assetBundleName,
				assetNames = assetPaths,
				addressableNames = addressableNames
			});
		}
		CompatibilityBuildPipeline.BuildAssetBundles(buildPath, builds.ToArray(), BUILD_OPTIONS, AirshipPlatformUtil.ToBuildTarget(platform));

		Debug.Log($"[Editor]: Finished building {platform} asset bundles in {sw.Elapsed.TotalSeconds} seconds.");
	}

	public static void BuildLocalAssetBundles()
	{
		BuildGameAssetBundles(AirshipPlatformUtil.GetLocalPlatform());
	}

	[MenuItem("Airship/📁 Misc/Build Local AssetBundles", priority = 311)]
	public static void BuildLocalAssetBundlesMenuItem() {
		var platform = AirshipPlatformUtil.FromRuntimePlatform(Application.platform);
		BuildPlatforms(new[] {platform});
	}

	[MenuItem("Airship/📁 Misc/Delete Local AssetBundles", priority = 312)]
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

	[MenuItem("Airship/📁 Misc/Build All AssetBundles", priority = 310)]
	public static void BuildAllAssetBundles() {
		BuildPlatforms(AirshipPlatformUtil.livePlatforms);
	}

	public static bool BuildPlatforms(AirshipPlatform[] platforms) {
		var sw = Stopwatch.StartNew();
		try
		{
			foreach (var platform in platforms) {
				BuildGameAssetBundles(platform);
			}

			Debug.Log($"Rebuilt game asset bundles for {platforms.Length} platform{(platforms.Length > 1 ? "s" : "")} in {sw.Elapsed.TotalSeconds}s");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to build asset bundles. Message={e.Message}");
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
#if UNITY_EDITOR
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

			var config = AssetDatabase.LoadAssetAtPath<EasyEditorConfig>("Assets/EasyEditorConfig.asset");
			if (!config.buildBundlesOnPlay) return;

			Debug.Log("[EDITOR]: Building asset bundles..");
			CreateAssetBundles.BuildLocalAssetBundles();
		}
		else if (state == PlayModeStateChange.EnteredEditMode)
		{
			Debug.Log("Resetting scenes.");
			CreateAssetBundles.ResetScenes();
		}
	}
}