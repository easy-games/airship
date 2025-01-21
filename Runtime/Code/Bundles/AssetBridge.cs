using System;
using System.Collections.Generic;
using System.IO;
using Code.Bootstrap;
using JetBrains.Annotations;
using Luau;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

[LuauAPI]
[Preserve]
public class AssetBridge : IAssetBridge
{
	public static string GamesPath = Path.Join("bundles", "games");
	public static string PackagesPath = Path.Join("bundles", "packages");

	public static bool useBundles = true;

	private static readonly Lazy<AssetBridge> _instance = new(() => new AssetBridge());
	public static AssetBridge Instance => _instance.Value;

	[CanBeNull] private static GameConfig gameConfig;

	public AssetBundle GetAssetBundle(string name)
	{
		AssetBundle retValue = SystemRoot.Instance.loadedAssetBundles[name].assetBundle;
		return retValue;
	}

	public GameConfig LoadGameConfigAtRuntime() {
		if (gameConfig) {
			return gameConfig;
		}

// #if UNITY_EDITOR
// 		return GameConfig.Load();
// #endif

		gameConfig = this.LoadAssetIfExistsInternal<GameConfig>("Assets/GameConfig.asset");
		return gameConfig;
	}

	private Type GetTypeFromPath(string path)
	{
		var extension = Path.GetExtension(path);
		Type type = null;
		switch (extension)
		{
			case ".asset":
				type = typeof(ScriptableObject);
				break;
			case ".png":
			case ".jpg":
				type = typeof(Texture2D);
				break;
			case ".ogg":
			case ".mp3":
			case ".wav":
				type = typeof(AudioClip);
				break;
			case ".txt":
			case ".bytes":
				type = typeof(TextAsset);
				break;
			default:
				type = typeof(Object);
				break;
		}

		return type;
	}

	/// <summary>
	/// Used by TS.
	/// C# should use <see cref="LoadAssetInternal{T}" />
	/// </summary>
	public Object LoadAsset(string path) {
		if (path.EndsWith(".sprite")) {
			return LoadAssetInternal<Sprite>(path.Replace(".sprite", ""));
		}
		return LoadAssetInternal<Object>(path);
	}

	public Object LoadAssetIfExists(string path) {
		if (path.EndsWith(".sprite")) {
			return LoadAssetInternal<Sprite>(path.Replace(".sprite", ""), false);
		}
		return LoadAssetInternal<Object>(path, false);
	}

	public T LoadAssetIfExistsInternal<T>(string path) where T : Object
	{
		return LoadAssetInternal<T>(path, false);
	}

	public bool IsLoaded()
	{
		return SystemRoot.Instance != null;
	}

	public T GetBinaryFileFromLuaPath<T>(string luaPath) where T : Object {
		var root = SystemRoot.Instance;
		foreach (var scope in root.luauFiles.Keys) {
			var luauFiles = root.luauFiles[scope];
			if (luauFiles.TryGetValue(luaPath, out var binaryFile)) {
				return binaryFile as T;
			}
		}

		return null;
	}

	public T LoadAssetInternal<T>(string path, bool printErrorOnFail = true) where T : Object
	{
		/*
		 * Expected formats. There will always be an extension.
		 *
		 * Scripts:
		 * - Shared/Resources/TS/Match/MatchState.lua
		 * - @Easy/Core/Shared/Resources/TS/Util/Task.lua
		 * - Shared/Resources/rbxts_include/node_modules/@easy-games/flamework-core/out/init.lua
		 *
		 * Other:
		 * - Shared/Resources/Prefabs/GameUI/ShopItem.prefab
		 */

		path = path.ToLower();
		if (path == "shared/include/runtimelib.lua") {
			path = "shared/resources/include/runtimelib.lua";
		}

		if (path.StartsWith("assets/")) {
			path = path.Substring(7);
		}
		// Correct package path
		if (path.StartsWith("@")) {
			path = "airshippackages/" + path;
		}

		string importedPackageName; // ex: "@Easy/Core" or "" for game package.
		bool isImportedPackage;
		string assetBundleFile;
		if (path.StartsWith("airshippackages/@")) {
			var split = path.Split("/");
			if (split.Length < 3) {
				if (printErrorOnFail) {
					Debug.LogError($"Failed to load invalid asset path: \"{path}\"");
				}
				return null;
			}
			
			// split should be of form [AirshipPackages, @Easy, Core, ...]
			importedPackageName = split[1] + "/" + split[2];
			isImportedPackage = true;
			assetBundleFile = "shared/resources";
		} else {
			importedPackageName = "";
			isImportedPackage = false;
			assetBundleFile = "shared/resources";
		}

		SystemRoot root = SystemRoot.Instance;

		if (root != null && Application.isPlaying) {
			string fullFilePath = path;
			if (!path.StartsWith("assets/")) {
				fullFilePath = $"assets/{path}";
			}

			// find luau file from code.zip
			if (path.EndsWith(".lua")) {
				var scriptFile = this.GetBinaryFileFromLuaPath<AirshipScript>(fullFilePath);
				if (scriptFile) {
					return scriptFile as T;
				}

				if (!Application.isEditor) {
					Debug.LogError("Unable to find luau file: " + fullFilePath);
					return null;
					// foreach (var pair in root.luauFiles) {
					// 	foreach (var filePair in pair.Value) {
					// 		Debug.Log("  - (" + pair.Key + ") " + filePair.Key);
					// 	}
					// }
				}
			}

			//determine the asset bundle via the prefix
			foreach (var bundleValue in root.loadedAssetBundles) {
				LoadedAssetBundle loadedBundle = bundleValue.Value;
				if (loadedBundle.assetBundle == null) {
					continue;
				}

				bool thisBundle = false;
				if (loadedBundle.airshipPackage.packageType == AirshipPackageType.Game) {
					if (!isImportedPackage && loadedBundle.assetBundleFile.ToLower() == assetBundleFile) {
						thisBundle = true;
					}
				} else if (loadedBundle.airshipPackage.packageType == AirshipPackageType.Package) {
					// Debug.Log($"importedPackageName={importedPackageName}, bundleId={loadedBundle.bundleId.ToLower()}");
					if (isImportedPackage && loadedBundle.bundleId.ToLower() == importedPackageName &&
					    loadedBundle.assetBundleFile.ToLower() == assetBundleFile) {
						thisBundle = true;
					}
				}

				if (!thisBundle) {
					continue;
				}

				if (loadedBundle.assetBundle.Contains(fullFilePath)) {
					if (RunCore.IsServer()) {
						// Debug.Log($"Loading asset {fullFilePath}");
					}
					return loadedBundle.assetBundle.LoadAsset<T>(fullFilePath);
				} else {
					if (printErrorOnFail) {
						// Debug.Log("Listing all files:");
						// foreach (var asset in loadedBundle.assetBundle.GetAllAssetNames()) {
						// 	Debug.Log("  - " + asset);
						// }
						Debug.LogError("Asset file not found: " + path + " (Attempted to load it from " + loadedBundle.bundleId + "/" + loadedBundle.assetBundleFile + "). Make sure to include a file extension (for example: .prefab)");
					}
					return null;
				}
			}
		}

#if UNITY_EDITOR && !AIRSHIP_PLAYER
		//Check the resource system
		Profiler.BeginSample("Editor.AssetBridge.LoadAsset");

		var fixedPath = $"assets/{path.ToLower()}";
		fixedPath = fixedPath.Replace(".lua", ".ts");

		if (!(fixedPath.StartsWith("assets/resources") || fixedPath.StartsWith("assets/airshippackages"))) {
			if (path != "gameconfig.asset") {
				Profiler.EndSample();
				Debug.LogError($"Failed to load asset at path: \"{fixedPath}\". Tried to load asset outside of a valid folder. Runtime loaded assets must be in either \"Assets/Resources\" or \"Assets/AirshipPackages\"");
				return null;
			}

		}

		var res = AssetDatabase.LoadAssetAtPath<T>(fixedPath);

		if (res != null) {
			Profiler.EndSample();
			return res;
		}

		Profiler.EndSample();
#endif

		if (printErrorOnFail) {
			Debug.LogError("AssetBundle file not found: " + path + " (No asset bundle understood this path - is this asset bundle loaded?)");
		}
		return null;
	}

	public string[] GetAllBundlePaths()
	{ 
        //Get a list of directories in Assets
        string[] directories = Directory.GetDirectories("Assets", "*", SearchOption.TopDirectoryOnly);
        
		//Get a list of bundles in each game
        List<string> bundles = new List<string>();
        foreach (string directory in directories)
        {
            string combinedPath = Path.Combine(directory, "AirshipPackages");
            bundles.AddRange(Directory.GetDirectories(combinedPath, "*", SearchOption.TopDirectoryOnly));
        }
		return bundles.ToArray();	
    }

    public string[] GetAllGameRootPaths()
    {
        //Get a list of directories in Assets
        string[] directories = Directory.GetDirectories("Assets", "*", SearchOption.TopDirectoryOnly);
        return directories;
    }

    public string[] GetAllAssets()
	{
		List<string> results = new();
		foreach (var bundle in SystemRoot.Instance.loadedAssetBundles)
		{
			results.AddRange(bundle.Value.assetBundle.GetAllAssetNames());
		}

		return results.ToArray();
	}
}