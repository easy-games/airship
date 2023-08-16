using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Agones;
using Agones.Model;
using Code.Bootstrap;
using Code.GameBundle;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using Tayx.Graphy;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

[Serializable]
public struct StartupConfig
{
	public string CoreBundleId; // core, for now
	public string CoreBundleVersion; // UUID
	public string GameBundleId; // bedwars but could be islands, etc
	public string GameBundleVersion; // UUID
	public string StartingSceneName; // BWMatchScene
	public string CdnUrl; // Base url where we download bundles
	public string[] ClientBundles;
	public string[] SharedBundles;
	public List<AirshipPackageDocument> packages;
}

public class ServerBootstrap : MonoBehaviour
{
	[NonSerialized] public StartupConfig startupConfig;
	public PlayerConfig playerConfig;

	[Header("Editor only settings.")]
	public string overrideGameBundleId;
	public string overrideGameBundleVersion;
	public string overrideCoreBundleId;
	public string overrideCoreBundleVersion;
	public string overrideStartingScene = "BWMatchScene";
	public string overrideQueueType = "CLASSIC_SQUADS";
	public bool downloadBundles = false;

	[NonSerialized] private AgonesSdk _agones;
	private bool _launchedServer = false;

	private string _queueType = "";

    [NonSerialized] private string _joinCode = "";

    public EasyEditorConfig editorConfig;

    public bool serverReady = false;
    public event Action onStartLoadingGame;
    public event Action onServerReady;

    private void Awake()
    {
        if (RunCore.IsClient())
        {
            Debug.Log("This is a client.");
            return;
        }
        serverReady = false;
        Debug.Log("This is a server.");

		Application.targetFrameRate = 90;

		_queueType = overrideQueueType;

		_agones = FindObjectOfType<AgonesSdk>();
		_agones.enabled = true;
		SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
	}

	private void Start()
	{
		if (RunCore.IsClient())
		{
			return;
		}

		InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;

		if (RunCore.IsEditor())
		{
			InstanceFinder.ServerManager.StartConnection();
		}
		else
		{
			InstanceFinder.ServerManager.StartConnection(7654);
		}

		GraphyManager.Instance.Enable();
		GraphyManager.Instance.AdvancedModuleState = GraphyManager.ModuleState.OFF;
	}

	private void OnDisable()
	{
		if (RunCore.IsClient()) return;

		SceneManager.sceneLoaded -= SceneManager_OnSceneLoaded;
		if (InstanceFinder.ServerManager)
		{
			InstanceFinder.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
		}
	}

	private void SceneManager_OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		SceneManager.SetActiveScene(scene);
	}

	public bool IsAgonesEnvironment()
	{
		return Environment.GetEnvironmentVariable("KUBERNETES_PORT") != null;
	}

	private async void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
	{
		if (args.ConnectionState == LocalConnectionState.Started)
		{
			// Server has bound to port.

			InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData("CoreScene"));

			if (this.IsAgonesEnvironment())
			{
				var success = await _agones.Connect();
				if (!success)
				{
					Debug.LogError("Failed to connect to Agones SDK server.");
					return;
				}
			}

			startupConfig = new StartupConfig()
			{
				GameBundleId = overrideGameBundleId,
				GameBundleVersion = overrideGameBundleVersion,
				CoreBundleId = overrideCoreBundleId,
				CoreBundleVersion = overrideCoreBundleVersion,
				StartingSceneName = overrideStartingScene,
				CdnUrl = "https://gcdn-staging.easy.gg",
				ClientBundles = new[]
				{
					"coreclient/resources",
					"coreclient/scenes",
					"client/resources",
					"client/scenes",
				},
				SharedBundles = new[]
				{
					"coreshared/resources",
					"coreshared/scenes",
					"shared/resources",
					"shared/scenes",
				},
			};

			if (this.IsAgonesEnvironment())
			{
				// Wait for queue configuration to hit agones.
				var gameServer = await _agones.GameServer();
				OnGameServerChange(gameServer);

				_agones.WatchGameServer(OnGameServerChange);

				await _agones.Ready();
			}
			else
			{
#if UNITY_EDITOR
				var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
				startupConfig.packages = gameConfig.packages;
#endif

				StartCoroutine(LoadWithStartupConfig(null));
			}
		}
	}

	/**
     * Called whenever we receive GameServer changes from Agones.
     */
	private void OnGameServerChange(GameServer server)
	{
		if (_launchedServer) return;

		var annotations = server.ObjectMeta.Annotations;

		if (annotations.ContainsKey("GameBundleId"))
		{
			Debug.Log($"[Agones]: Server will run game {annotations["GameBundleId"]}_v{annotations["GameBundleVersion"]}");
			_launchedServer = true;
			startupConfig.GameBundleId = annotations["GameBundleId"];
			startupConfig.GameBundleVersion = annotations["GameBundleVersion"];
			startupConfig.CoreBundleId = annotations["CoreBundleId"];
			startupConfig.CoreBundleVersion = annotations["CoreBundleVersion"];
			startupConfig.StartingSceneName = annotations["StartingSceneName"];
			_joinCode = annotations["ShareCode"];

			if (annotations.TryGetValue("QueueId", out string id))
			{
				_queueType = id;
			}

			var urlAnnotations = new string[]
			{
				"resources",
				"resources.manifest",
				"scenes",
				"scenes.manifest"
			};

			var privateRemoteBundleFiles = new List<RemoteBundleFile>();

			// Download game's private server bundles
			foreach (var annotation in urlAnnotations)
			{
				var url = annotations[$"{startupConfig.GameBundleId}_{annotation}"];
				var fileName = $"server/{annotation}"; // IE. resources, resources.manifest, etc

				Debug.Log($"Adding private remote bundle file. bundleId: {startupConfig.GameBundleVersion}, annotation: {annotation}, url: {url}");

				privateRemoteBundleFiles.Add(new RemoteBundleFile(
					fileName,
					url,
					startupConfig.GameBundleId,
					startupConfig.GameBundleVersion
				));
			}

			StartCoroutine(LoadRemoteGameId(privateRemoteBundleFiles));
		}
	}

	/**
	 * Called after Agones annotations are loaded.
	 */
	private IEnumerator LoadRemoteGameId(List<RemoteBundleFile> privateRemoteBundleFiles) {
		onStartLoadingGame?.Invoke();;
		// StartupConfig is safe to use in here.

		// Download game config
		var url = $"{startupConfig.CdnUrl}/game/{startupConfig.GameBundleId}/{startupConfig.GameBundleVersion}/gameConfig.json";
		var request = new UnityWebRequest(url);
		var gameConfigPath = Path.Join(AssetBridge.GamesPath, startupConfig.GameBundleId, "gameConfig.json");
		request.downloadHandler = new DownloadHandlerFile(gameConfigPath);
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success) {
			Debug.LogError($"Failed to download gameConfig.json. url={url}, message={request.error}");
			Debug.Log("Retrying in 1s...");

			// Retry
			yield return new WaitForSeconds(1);
			yield return LoadRemoteGameId(privateRemoteBundleFiles);
			yield break;
		}

		using var sr = new StreamReader(gameConfigPath);
		var jsonString = sr.ReadToEnd();
		var gameConfig = JsonUtility.FromJson<GameConfigDto>(jsonString);

		this.startupConfig.packages = new();
		this.startupConfig.packages.Add(new AirshipPackageDocument() {
			id = this.startupConfig.CoreBundleId,
			version = this.startupConfig.CoreBundleVersion,
		});
		foreach (var package in gameConfig.packages) {
			if (package.id == "Core") continue;
			startupConfig.packages.Add(package);
		}
		this.startupConfig.packages.Add(new AirshipPackageDocument() {
			id = this.startupConfig.GameBundleId,
			version = this.startupConfig.GameBundleVersion,
		});

		yield return LoadWithStartupConfig(privateRemoteBundleFiles.ToArray());
	}

	/**
     * Called once we have loaded all of StartupConfig from Agones & other sources.
     */
	private IEnumerator LoadWithStartupConfig(RemoteBundleFile[] privateBundleFiles)
	{
		var clientBundleLoader = FindObjectOfType<ClientBundleLoader>();
		clientBundleLoader.SetStartupConfig(startupConfig);

		List<AirshipPackage> packages = new();
		foreach (var airshipPackageDoc in startupConfig.packages) {
			packages.Add(new AirshipPackage(airshipPackageDoc.id, airshipPackageDoc.version, airshipPackageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
		}

		// Download bundles over network
		if (!RunCore.IsEditor() || downloadBundles)
		{
			var bundleDownloader = FindObjectOfType<BundleDownloader>();

			packages.Clear();
			packages.Add(new AirshipPackage(startupConfig.CoreBundleId, startupConfig.CoreBundleVersion, AirshipPackageType.Package));
			foreach (var package in startupConfig.packages) {
				packages.Add(new AirshipPackage(package.id, package.version, AirshipPackageType.Package));
			}
			packages.Add(new AirshipPackage(startupConfig.GameBundleId, startupConfig.GameBundleVersion, AirshipPackageType.Game));

			yield return bundleDownloader.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), privateBundleFiles);
		}

        print("[Server Bootstrap]: Loading game bundle: " + startupConfig.GameBundleId);
        yield return SystemRoot.Instance.LoadBundles(startupConfig.GameBundleId, this.editorConfig, packages);
        
        Debug.Log("[Server Bootstrap]: Loading scene " + startupConfig.StartingSceneName + "...");
        var st = Stopwatch.StartNew();
        var sceneLoadData = new SceneLoadData(startupConfig.StartingSceneName);
        sceneLoadData.ReplaceScenes = ReplaceOption.None;
        InstanceFinder.SceneManager.LoadConnectionScenes(sceneLoadData);
        Debug.Log("[Server Bootstrap]: Finished loading scene in " + st.ElapsedMilliseconds + "ms.");

        serverReady = true;
        onServerReady?.Invoke();
    }

	public void Shutdown()
	{
		if (_agones)
		{
			_agones.Shutdown();
		}
	}

	/**
     * This is called from TS when we are ready to accept connections.
     */
	public void FinishedSetup()
	{
		// if (!RunCore.IsEditor())
		// {
		//     _agones.Ready();
		// }
	}

	public string GetQueueType()
	{
		return _queueType;
	}

	public string GetJoinCode()
	{
		return _joinCode;
	}
}