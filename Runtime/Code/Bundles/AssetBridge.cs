using System;
using System.Collections.Generic;
using System.IO;
using Code.Bootstrap;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

[LuauAPI]
[Preserve]
public static class AssetBridge
{
	public static string GamesPath = Path.Join(Application.persistentDataPath, "Games");
	public static string PackagesPath = Path.Join(Application.persistentDataPath, "Packages");

	public static bool useBundles = true;

	public static AssetBundle GetAssetBundle(string name)
	{
		AssetBundle retValue = SystemRoot.Instance.loadedAssetBundles[name].assetBundle;
		return retValue;
	}

	private static Type GetTypeFromPath(string path)
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
	public static Object LoadAsset(string path)
	{
		return LoadAssetInternal<Object>(path);
	}

	public static Object LoadAssetIfExists(string path)
	{
		return LoadAssetInternal<Object>(path, false);
	}

	public static T LoadAssetIfExistsInternal<T>(string path) where T : Object
	{
		return LoadAssetInternal<T>(path, false);
	}

	public static bool IsLoaded()
	{
		return SystemRoot.Instance != null;
	}

	public static T LoadAssetInternal<T>(string path, bool printErrorOnFail = true) where T : Object
	{
		/*
		 * Expected formats.
		 *
		 * Scripts:
		 * - Shared/Resources/TS/Match/MatchState
		 * - Imports/Core/Shared/Resources/TS/Util/Task
		 * - Shared/Resources/rbxts_include/node_modules/@easy-games/flamework-core/out/init
		 *
		 * Other:
		 * - Shared/Resources/Prefabs/GameUI/ShopItem.prefab
		 */

		path = path.ToLower();
		var split = path.Split("/");

		if (split.Length < 3) {
			if (printErrorOnFail)
			{
				Debug.LogError($"Failed to load invalid asset path: \"{path}\"");
			}
			return null;
		}

		string importedPackageName; // ex: "Core" or "" for game package.
		bool isImportedPackage;
		string assetBundleFile; // ex: "Shared/Resources" or "" for game package.
		if (split[0] == "imports" && split.Length >= 4) {
			importedPackageName = split[1];
			isImportedPackage = true;
			assetBundleFile = split[2] + "/" + split[3];
		} else {
			importedPackageName = "";
			isImportedPackage = false;
			assetBundleFile = split[0] + "/" + split[1];
		}
		Debug.Log($"importedPackageName={importedPackageName}, assetBundleFile={assetBundleFile}");

		SystemRoot root = SystemRoot.Instance;

		if (root != null && Application.isPlaying)
		{
			//determine the asset bundle via the prefix
			foreach (var bundleValue in root.loadedAssetBundles)
			{
				LoadedAssetBundle loadedBundle = bundleValue.Value;
				if (loadedBundle.assetBundle == null)
				{
					continue;
				}

				bool thisBundle = false;
				if (loadedBundle.airshipPackage.packageType == AirshipPackageType.Game) {
					if (!isImportedPackage && loadedBundle.assetBundleFile.ToLower() == assetBundleFile) {
						thisBundle = true;
					}
				} else if (loadedBundle.airshipPackage.packageType == AirshipPackageType.Package) {
					if (isImportedPackage && loadedBundle.bundleId.ToLower() == importedPackageName &&
					    loadedBundle.assetBundleFile.ToLower() == assetBundleFile) {
						thisBundle = true;
					}
				}

				if (!thisBundle)
				{
					continue;
				}

				string file = Path.Combine("assets", "bundles", path);
				Debug.Log("file: " + file);

				if (loadedBundle.assetBundle.Contains(file))
				{
					return loadedBundle.assetBundle.LoadAsset<T>(file);
				}
				else
				{
					if (printErrorOnFail)
					{
						Debug.LogError("AssetBundle file not found: " + path + " (Attempted to load it from " + loadedBundle.bundleId + "/" + loadedBundle.assetBundleFile + ")");
					}
					return null;
				}
			}
		}

#if UNITY_EDITOR
		//Check the resource system

		//Get path without extension
		Profiler.BeginSample("Editor.AssetBridge.LoadAsset");

		// Assets/Game/Core/Bundles/CoreShared/Resources/TS/Main.lua
		//var fixedPath = $"Assets/Game/{(isCore ? "core" : "bedwars")}/Bundles/{path}".ToLower();

		// NOTE: For now, we're just building the core bundles into the game's bundle folder.
		var fixedPath = $"assets/bundles/{path}".ToLower();

		// if (!fixedPath.Contains("/resources/"))
		// {
		// 	fixedPath = fixedPath.Replace("/ts/", "/resources/ts/");
		// 	fixedPath = fixedPath.Replace("/include/", "/resources/include/");
		// 	fixedPath = fixedPath.Replace("/rbxts_include/", "/resources/rbxts_include/");
		// }
		
		//Debug.Log($"path: {path}, newPath: {newPath}");

		var res = AssetDatabase.LoadAssetAtPath<T>(fixedPath);

		if (res != null)
		{
			Profiler.EndSample();
			return res;
		}

		Profiler.EndSample();
#endif


		if (printErrorOnFail)
		{
			Debug.LogError("AssetBundle file not found: " + path + " (No asset bundle understood this path - is this asset bundle loaded?)");
		}
		return null;
	}

	public static string[] GetAllBundlePaths()
	{ 
        //Get a list of directories in Assets
        string[] directories = Directory.GetDirectories("Assets", "*", SearchOption.TopDirectoryOnly);
        
		//Get a list of bundles in each game
        List<string> bundles = new List<string>();
        foreach (string directory in directories)
        {
            string combinedPath = Path.Combine(directory, "Bundles");
            bundles.AddRange(Directory.GetDirectories(combinedPath, "*", SearchOption.TopDirectoryOnly));
        }
		return bundles.ToArray();	
    }

    public static string[] GetAllGameRootPaths()
    {
        //Get a list of directories in Assets
        string[] directories = Directory.GetDirectories("Assets", "*", SearchOption.TopDirectoryOnly);
        return directories;
    }

    public static string[] GetAllAssets()
	{
		List<string> results = new();
		foreach (var bundle in SystemRoot.Instance.loadedAssetBundles)
		{
			results.AddRange(bundle.Value.assetBundle.GetAllAssetNames());
		}

		return results.ToArray();
	}
}