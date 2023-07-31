using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.VisualScripting;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class CreateAssetBundles {
	public const BuildAssetBundleOptions BUILD_OPTIONS = BuildAssetBundleOptions.UncompressedAssetBundle;

	private static void FixBundleNames()
	{
		var relativeBundlePathInfos = new Dictionary<string, bool>()
		{
			// Core
			{ $"coreclient/resources", true },
			{ $"coreclient/scenes", true },
			{ $"coreserver/resources", true },
			{ $"coreserver/scenes", true },
			{ $"coreshared/resources", true },
			{ $"coreshared/scenes", true },

			// Game
			{ $"client/resources", false },
			{ $"client/scenes", false },
			{ $"server/resources", false },
			{ $"server/scenes", false },
			{ $"shared/resources", false },
			{ $"shared/scenes", false },
		};

		foreach (var relativeBundlePathInfo in relativeBundlePathInfos)
		{
			// Example Core Path: "assets/game/bedwars/bundles/coreclient/resources"
			// NOTE: For now, we're building core bundles into the game's bundles folder.
			// Example Game Path: "assets/game/bedwars/bundles/client/resources"
			var rootPath = relativeBundlePathInfo.Value ?
				BootstrapHelper.CoreBundleRelativeRootPath :
				BootstrapHelper.GameBundleRelativeRootPath;

			var assetImporter = AssetImporter.GetAtPath($"{Path.Combine(rootPath, relativeBundlePathInfo.Key)}");

			assetImporter.assetBundleName = relativeBundlePathInfo.Key;
		}
	}

	public static void BuildLocalAssetBundles()
	{
		FixBundleNames();

		var sw = Stopwatch.StartNew();

		if (!Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.CreateDirectory(AssetBridge.BundlesPath);
		}

		var localPath = Path.Combine(AssetBridge.BundlesPath, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		Debug.Log("[EDITOR]: Building AssetBundles into folder: " + localPath);
		BuildPipeline.BuildAssetBundles(localPath, BUILD_OPTIONS, EditorUserBuildSettings.activeBuildTarget);
		Debug.Log($"[EDITOR]: Built asset bundles in {sw.ElapsedMilliseconds} ms");
		
		MoveAssetBundles();
	}

	private static void MoveAssetBundles()
	{
		Debug.Log("[EDITOR]: Moving asset bundles to destination folders.");

		var sw = Stopwatch.StartNew();

		var localPath = Path.Combine(AssetBridge.BundlesPath, "local");
		var dirs = Directory.GetDirectories(localPath);

		//Debug.Log($"[EDITOR]: CreateAssetBundles() dirs:{Environment.NewLine}{dirs.ToCommaSeparatedString()}");

		var corePath = Path.Combine(AssetBridge.BundlesPath, BootstrapHelper.CoreBundleId);
		var gamePath = Path.Combine(AssetBridge.BundlesPath, BootstrapHelper.GameBundleId);
		 
		foreach (var dir in dirs)
		{
			var dirSplit = dir.Split(Path.DirectorySeparatorChar);
			var dirName = dirSplit[dirSplit.Length - 1];
			var isCore = dirName.StartsWith(BootstrapHelper.CoreBundleId);
			var destPathRoot = isCore ? corePath : gamePath;
			var destPath = Path.Combine(destPathRoot, dirName);

			if (Directory.Exists(destPath))
			{
				Directory.Delete(destPath, recursive: true);
			}

			if (!Directory.Exists(destPathRoot))
			{
				Directory.CreateDirectory(destPathRoot);
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
		Debug.Log("Deleting local asset bundles in " + AssetBridge.BundlesPath);
		if (Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.Delete(AssetBridge.BundlesPath, true);
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
		string assetBundleDirectory = AssetBridge.BundlesPath;
		if (!Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.CreateDirectory(AssetBridge.BundlesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildPipeline.BuildAssetBundles(localPath, BUILD_OPTIONS, BuildTarget.StandaloneLinux64);
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
		string assetBundleDirectory = AssetBridge.BundlesPath;
		if (!Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.CreateDirectory(AssetBridge.BundlesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildPipeline.BuildAssetBundles(localPath, BUILD_OPTIONS, BuildTarget.StandaloneWindows64);
		Debug.Log($"Built assets in {sw.ElapsedMilliseconds} ms");

		MoveAssetBundles();
	}

	public static void BuildMacPlayerAssetBundlesAsLocal()
	{
		// This is commented out for this build because the linux build on github doesn't care about the
		// game assets. Those get baked in later.
		// FixBundleNames();

		var sw = Stopwatch.StartNew();
		string assetBundleDirectory = AssetBridge.BundlesPath;
		if (!Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.CreateDirectory(AssetBridge.BundlesPath);
		}
		string localPath = Path.Combine(assetBundleDirectory, "local");
		if (!Directory.Exists(localPath))
		{
			Directory.CreateDirectory(localPath);
		}

		BuildPipeline.BuildAssetBundles(localPath, BUILD_OPTIONS, BuildTarget.StandaloneOSX);
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

		var assetBundleDirectory = AssetBridge.BundlesPath;
		if (!Directory.Exists(AssetBridge.BundlesPath))
		{
			Directory.CreateDirectory(AssetBridge.BundlesPath);
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
					BuildPipeline.BuildAssetBundles(path, BUILD_OPTIONS, platform.Key);
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

	public static GameBundleConfig FindGameConfig()
	{
		return AssetDatabase.LoadAssetAtPath<GameBundleConfig>("Assets/GameConfig.asset");
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
			if (!config.useBundlesInEditor) return;

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