using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Agones;
using Agones.Model;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using Tayx.Graphy;
using UnityEngine;
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
}

public class ServerBootstrap : MonoBehaviour
{
	[NonSerialized] public StartupConfig StartupConfig;
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

			StartupConfig = new StartupConfig()
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

		Debug.Log("OnGameServerChange.1");

		var annotations = server.ObjectMeta.Annotations;

		if (annotations.ContainsKey("GameBundleId"))
		{
			Debug.Log("OnGameServerChange.2");
			Debug.Log($"[Agones]: Server will run Game Bundle {annotations["GameBundleId"]}");
			_launchedServer = true;
			StartupConfig.GameBundleId = annotations["GameBundleId"];
			StartupConfig.GameBundleVersion = annotations["GameBundleVersion"];
			StartupConfig.CoreBundleId = annotations["CoreBundleId"];
			StartupConfig.CoreBundleVersion = annotations["CoreBundleVersion"];
			StartupConfig.StartingSceneName = annotations["StartingSceneName"];
			_joinCode = annotations["ShareCode"];

			if (annotations.TryGetValue("QueueId", out string id))
			{
				_queueType = id;
			}

			var annotationPrefixes = new string[]
			{
				StartupConfig.CoreBundleId, // core
				StartupConfig.GameBundleId  // bedwars
			};

			var urlAnnotations = new string[]
			{
				"resources",
				"resources.manifest",
				"scenes",
				"scenes.manifest"
			};

			var remoteBundleFiles = new List<RemoteBundleFile>();

			foreach (var annotationPrefix in annotationPrefixes)
			{
				foreach (var annotation in urlAnnotations)
				{
					var url = annotations[$"{annotationPrefix}_{annotation}"];
					var filePath = $"server/{annotation}"; // IE. resources, resources.manifest, etc

					if (annotationPrefix == StartupConfig.CoreBundleId)
					{
						filePath = filePath.Replace("server/", "coreserver/");
					}

					Debug.Log($"ServerBootstrap.OnGameServerChange() downloading file. annotationPrefix: {annotationPrefix}, annotation: {annotation}, url: {url}");

					remoteBundleFiles.Add(new RemoteBundleFile(
						filePath,
						url,
						annotationPrefix));
				}
			}

			StartCoroutine(LoadWithStartupConfig(remoteBundleFiles.ToArray()));
		}
	}

	/**
     * Called once we have loaded all StartupConfig from Agones.
     */
	private IEnumerator LoadWithStartupConfig(RemoteBundleFile[] remoteBundleFiles)
	{
		var clientBundleLoader = FindObjectOfType<ClientBundleLoader>();
		clientBundleLoader.SetStartupConfig(StartupConfig);

		// Download bundles over network
		if (!RunCore.IsEditor() || downloadBundles)
		{
			var bundleDownloader = FindObjectOfType<BundleDownloader>();
			yield return bundleDownloader.DownloadBundles(StartupConfig, remoteBundleFiles);
		}

        print("[Server Bootstrap]: Loading game bundle: " + StartupConfig.GameBundleId);
        yield return SystemRoot.Instance.LoadBundles(StartupConfig.GameBundleId, this.editorConfig);
        
        Debug.Log("[Server Bootstrap]: Loading scene " + StartupConfig.StartingSceneName + "...");
        var st = Stopwatch.StartNew();
        var sceneLoadData = new SceneLoadData(StartupConfig.StartingSceneName);
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