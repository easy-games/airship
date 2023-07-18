using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using JetBrains.Annotations;
using UnityEngine;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;

public class SystemRoot : Singleton<SystemRoot>
{
	public class AssetBundleMetaData
	{
		public AssetBundle m_assetBundle;
		//A fully formed path looks like: "Assets/Game/GameName/Bundles/Client/Resources/TS/TestScripts/TestScript.lua"
		//                                 [           root            ][bundle or alias][    name without ext   ][ext]

		//A full path is required by unity to load the path
		//however we can process different formats to get what the user intended:
		//
		//      1: Start with the bundle alias:   "Client/TS/TestScripts/TestScript.lua"
		//      2: Start with the full bundle:    "Client/Resources/TS/TestScripts/TestScript.lua"

		public string m_pathRoot; //eg: "Assets/Game/GameName/Bundles"  <- path 
		public string m_name;     //eg: "Client/Resources"  <- actual bundle name in unity asset bundle editor
		public string m_alias;    //eg: "Client"            <- short name so we dont have to type /resources/ a lot

		public List<string> m_prefixes = new(); //Because we have a few combinations, we'll do some "StartsWith()" checks to see if they mean us

		public AssetBundleMetaData(string pathRoot, string name, string alias)
		{
			m_pathRoot = pathRoot.ToLower();
			m_name = name.ToLower();
			m_alias = alias.ToLower();

			if (m_alias == null)
			{
				m_alias = m_name;
			}

			m_prefixes.Add(Path.Combine(pathRoot, m_name)); // "Assets/Game/GameName/Bundles/Client/Resources"
			m_prefixes.Add(Path.Combine(pathRoot, m_alias)); // "Assets/Game/GameName/Bundles/Client"
			m_prefixes.Add(m_alias + "/"); // "Client/"
			m_prefixes.Add(m_name + "/"); // "Client/Resources/"
		}

		public bool PathBelongsToThisAssetBundle(string path)
		{
			foreach (string s in m_prefixes)
			{
				if (path.StartsWith(s))
				{
					return true;
				}
			}
			return false;
		}

		public string FixupPath(string sourcePath)
		{
			sourcePath = sourcePath.ToLower();

			if (sourcePath.StartsWith(m_pathRoot))
			{
				return sourcePath;
			}

			if (sourcePath.StartsWith(m_name))
			{
				//Case 2:  "Client/Resources/TS/TestScripts/TestScript.lua"
				//Client/Resources is m_name, the bundle name, so we're okay to use it as-is

				string trimmed = sourcePath.Substring(m_name.Length + 1);

				string bigPath = Path.Combine(m_pathRoot, m_name, trimmed);
				return bigPath;
			}

			if (sourcePath.StartsWith(m_alias))
			{
				//Case 1:"Client/TS/TestScripts/TestScript.lua" 
				//'Client' is the alias, needs to be unpacked to its bundle name 'Client/Resources'

				string trimmed = sourcePath.Substring(m_alias.Length + 1);

				string bigPath = Path.Combine(m_pathRoot, m_name, trimmed);
				return bigPath;
			}

			//Really an error case, we couldn't make a determination            
			return sourcePath;
		}
	}

	public Dictionary<string, AssetBundleMetaData> m_assetBundles = new Dictionary<string, AssetBundleMetaData>();

	private PrefabIdLoader _prefabIdLoader = new PrefabIdLoader();

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

	public IEnumerator LoadBundles(string game, EasyEditorConfig editorConfig)
	{
		var sw = Stopwatch.StartNew();

		var coreBundleId = BootstrapHelper.CoreBundleId;
		var gameBundleId = BootstrapHelper.GameBundleId;
		var coreBundleRootPath = BootstrapHelper.CoreBundleRelativeRootPath;
		var gameBundleRootPath = BootstrapHelper.GameBundleRelativeRootPath;

		List<IEnumerator> loadList1 = new();
		List<IEnumerator> loadList2 = new();

		// Resources
		var useBundles = IsUsingBundles(editorConfig);
		AssetBridge.useBundles = useBundles;
		print("is using bundles: " + useBundles);
		if (useBundles)
		{
			if (RunCore.IsClient())
			{
				loadList1.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreclient/resources", "coreclient", 1));
				loadList1.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"client/resources", "client", 2));
			}
			if (RunCore.IsServer())
			{
				loadList1.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreserver/resources", "coreserver", 3));
				loadList1.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"server/resources", "server", 4));
			}
			loadList1.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreshared/resources", "coreshared", 5));
			loadList1.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"shared/resources", "shared", 6));

			// Scenes
			loadList2.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreshared/scenes", null, 7));
			loadList2.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"shared/scenes", null, 8));
			if (RunCore.IsServer())
			{
				loadList2.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreserver/scenes", null, 9));
				loadList2.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"server/scenes", null, 10));
			}
			if (RunCore.IsClient())
			{
				loadList2.Add(LoadAssetBundle(coreBundleRootPath, isCore: true, $"coreclient/scenes", null, 11));
				loadList2.Add(LoadAssetBundle(gameBundleRootPath, isCore: false, $"client/scenes", null, 12));
			}

			yield return this.WaitAll(loadList1.ToArray());
			yield return this.WaitAll(loadList2.ToArray());
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
		Debug.Log("----- Network Objects -----");
		foreach (var collectionId in InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs.Keys)
		{
			var singlePrefabObjects = (SinglePrefabObjects)InstanceFinder.NetworkManager.RuntimeSpawnablePrefabs[collectionId];
			for (int i = 0; i < singlePrefabObjects.Prefabs.Count; i++)
			{
				var nob = singlePrefabObjects.Prefabs[i];
				Debug.Log(nob.gameObject.name + " collId=" + collectionId + " objectId=" + nob.ObjectId);
			}
		}
		Debug.Log("----------");


		Debug.Log("Finished loading asset bundles in " + sw.ElapsedMilliseconds + "ms.");
	}

	public void UnloadBundles()
	{
		foreach (var pair in m_assetBundles)
		{
			pair.Value.m_assetBundle.Unload(true);
			pair.Value.m_assetBundle = null;
		}
		m_assetBundles.Clear();
	}

	private IEnumerator LoadAssetBundle(string pathRoot, bool isCore, string name, string alias, ushort netCollectionId)
	{
		if (alias == null)
		{
			alias = name;
		}

		var bundleId = isCore ? BootstrapHelper.CoreBundleId : BootstrapHelper.GameBundleId;
		var bundleFilePath = Path.Combine(AssetBridge.BundlesPath, bundleId, name);
		Debug.Log($"bundleFilePath: {bundleFilePath}");
		var bundleCreateRequest = AssetBundle.LoadFromFileAsync(bundleFilePath);
		yield return bundleCreateRequest;

		var assetBundle = bundleCreateRequest.assetBundle;
		if (assetBundle == null)
		{
			Debug.LogError($"AssetBundle failed to load. name: {name}, bundleFilePath: {bundleFilePath}");
		}

		var bundleMetaData = new AssetBundleMetaData(pathRoot, name, alias)
		{
			m_assetBundle = assetBundle
		};

		m_assetBundles.Add(alias, bundleMetaData);

		yield return _prefabIdLoader.LoadNetworkObjects(assetBundle, netCollectionId);
	}
}
