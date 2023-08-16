using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Editor.Packages;
using Unity.VisualScripting;
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
		foreach (var gameBundlePath in gameBundles) {
			var assetImporter = AssetImporter.GetAtPath(Path.Combine(BootstrapHelper.GameBundleRelativeRootPath, gameBundlePath));
			assetImporter.assetBundleName = gameBundlePath;
		}

		string[] importFolders = AssetDatabase.GetSubFolders(BootstrapHelper.ImportsBundleRelativeRootPath);
		foreach (var importFolder in importFolders) {
			Debug.Log("import folder: " + importFolder);
			var split = importFolder.Split(Path.DirectorySeparatorChar);
			var importFolderName = split[split.Length - 1];
			foreach (var bundle in gameBundles) {
				var bundlePath = Path.Join(importFolder, bundle);
				if (!Directory.Exists(bundlePath)) {
					throw new Exception($"Package folder \"${importFolderName}/{bundle}\" was missing. Please create it. Folder path: {bundlePath}");
				}
				var assetImporter = AssetImporter.GetAtPath(bundlePath);
				assetImporter.assetBundleName =  $"{importFolderName}_{bundle}";
			}
		}
	}

	private static void  BuildGameAssetBundles(string path, BuildTarget buildTarget) {
		List<AssetBundleBuild> builds = new();
		foreach (var assetBundleFile in AirshipPackagesWindow.assetBundleFiles) {
			var assetBundleName = assetBundleFile.ToLower();
			var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
			builds.Add(new AssetBundleBuild() {
				assetBundleName = assetBundleName,
				assetNames = assetPaths
			});
		}
		BuildPipeline.BuildAssetBundles(path, builds.ToArray(), BUILD_OPTIONS, buildTarget);
	}

	public static void BuildLocalAssetBundles()
	{
		FixBundleNames();

		var sw = Stopwatch.StartNew();

		if (!Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.CreateDirectory(AssetBridge.GamesPath);
		}

		var localPath = Path.Combine(AssetBridge.GamesPath, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		Debug.Log("[EDITOR]: Building AssetBundles into folder: " + localPath);
		BuildGameAssetBundles(localPath, EditorUserBuildSettings.activeBuildTarget);
		Debug.Log($"[EDITOR]: Built asset bundles in {sw.ElapsedMilliseconds} ms");
		
		MoveAssetBundles();
	}

	private static void MoveAssetBundles()
	{
		Debug.Log("[EDITOR]: Moving asset bundles to destination folders.");

		var sw = Stopwatch.StartNew();

		var localPath = Path.Combine(AssetBridge.GamesPath, "local");
		var dirs = Directory.GetDirectories(localPath);

		var importsPath = Path.Combine(AssetBridge.GamesPath, "imports");
		var gamePath = Path.Combine(AssetBridge.GamesPath, BootstrapHelper.GameBundleId);

		foreach (var dir in dirs)
		{
			var dirSplit = dir.Split(Path.DirectorySeparatorChar);
			var dirName = dirSplit[dirSplit.Length - 1];
			var isImport = dirName.Contains("_");
			string destPath;
			string destPathParent;

			if (isImport) {
				var underscoreSplit = dirName.Split("_");
				var importName = underscoreSplit[0];
				var bundleName = underscoreSplit[1];
				destPathParent = Path.Combine(importsPath, importName);
				destPath = Path.Combine(importsPath, importName, bundleName);
			} else {
				destPathParent = Path.Combine(gamePath);
				destPath = Path.Combine(gamePath, dirName);
			}
			Debug.Log("Moving " + dir + " to " + destPath);

			if (Directory.Exists(destPath))
			{
				Directory.Delete(destPath, recursive: true);
			}

			if (!Directory.Exists(destPathParent))
			{
				Directory.CreateDirectory(destPathParent);
			}

			Directory.Move(dir, destPath);
		}

		Debug.Log($"[EDITOR]: Done moving asset bundles to destination folder. Took {sw.ElapsedMilliseconds} ms.");
	}

	[MenuItem("Airship/📁 Misc/Build Local AssetBundles", priority = 311)]
	public static void BuildLocalAssetBundlesMenuItem()
	{
		BuildSelectAssetBundles(true);
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
		// This is commented out for this build because the linux build on github doesn't care about the
		// game assets. Those get baked in later.
		// FixBundleNames();

		var sw = Stopwatch.StartNew();
		string assetBundleDirectory = AssetBridge.GamesPath;
		if (!Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.CreateDirectory(AssetBridge.GamesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildGameAssetBundles(localPath, BuildTarget.StandaloneLinux64);
		Debug.Log($"Built assets in {sw.ElapsedMilliseconds} ms");

		MoveAssetBundles();
	}

	// [MenuItem("Airship/Custom Local Bundle/Windows")]
	public static void BuildWindowsPlayerAssetBundlesAsLocal()
	{
		// This is commented out for this build because the linux build on github doesn't care about the
		// game assets. Those get baked in later.
		// FixBundleNames();

		var sw = Stopwatch.StartNew();
		string assetBundleDirectory = AssetBridge.GamesPath;
		if (!Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.CreateDirectory(AssetBridge.GamesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildGameAssetBundles(localPath, BuildTarget.StandaloneWindows64);
		Debug.Log($"Built assets in {sw.ElapsedMilliseconds} ms");

		MoveAssetBundles();
	}

	public static void BuildMacPlayerAssetBundlesAsLocal()
	{
		// This is commented out for this build because the linux build on github doesn't care about the
		// game assets. Those get baked in later.
		// FixBundleNames();

		var sw = Stopwatch.StartNew();
		string assetBundleDirectory = AssetBridge.GamesPath;
		if (!Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.CreateDirectory(AssetBridge.GamesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildGameAssetBundles(localPath, BuildTarget.StandaloneOSX);
		Debug.Log($"Built assets in {sw.ElapsedMilliseconds} ms");

		MoveAssetBundles();
	}

	[MenuItem("Airship/📁 Misc/Build All AssetBundles", priority = 310)]
	public static void BuildAllAssetBundles()
	{
		BuildSelectAssetBundles(
			doLocal: true,
			new Dictionary<BuildTarget, string>() {
				{ BuildTarget.StandaloneWindows, "windows" },
				{ BuildTarget.iOS, "ios" },
				{ BuildTarget.Android, "android" },
				{ BuildTarget.StandaloneLinux64, "linux" },
				{ BuildTarget.StandaloneOSX, "mac" }
			});
	}

	public static void BuildSelectAssetBundles(bool doLocal, Dictionary<BuildTarget, string> platforms = null)
	{
		FixBundleNames();

		var sw = Stopwatch.StartNew();

		var assetBundleDirectory = AssetBridge.GamesPath;
		if (!Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.CreateDirectory(AssetBridge.GamesPath);
		}

		ResetScenes();

		try
		{
			if (doLocal)
			{
				BuildLocalAssetBundles();
			}

			if (platforms != null)
			{
				foreach (var platform in platforms)
				{
					var path = Path.Combine(assetBundleDirectory, platform.Value);
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);

					}
					BuildGameAssetBundles(path, platform.Key);
				}
			}

			Debug.Log($"Rebuilt all asset bundles in {sw.Elapsed.TotalSeconds}s");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to build asset bundles. Message={e.Message}");
		}

		AddAllGameBundleScenes();
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