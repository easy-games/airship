using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using Agones;
using Agones.Model;
using Code.Analytics;
using Code.Bootstrap;
using Code.GameBundle;
using Code.Http.Internal;
using Code.Platform.Shared;
using Code.State;
using JetBrains.Annotations;
using kcp2k;
using Mirror;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

[Serializable]
public struct StartupConfig {
	public string GameBundleId; // bedwars but could be islands, etc
	[FormerlySerializedAs("GameBundleVersion")] public string GameAssetVersion; // UUID
	public string GameCodeVersion;
	public string StartingSceneName;
	public string GamePublishVersion;
	public string CdnUrl; // Base url where we download bundles
	public List<AirshipPackageDocument> packages;
}

[LuauAPI]
public class ServerBootstrap : MonoBehaviour
{
	[NonSerialized] public StartupConfig startupConfig;

	[Header("Editor only settings.")] public string overrideGameBundleId;
	public string overrideGameBundleVersion;

	public string airshipJWT;

	[SerializeField] public AgonesBetaSdk agones;

	public bool allocatedByAgones = false;

	[NonSerialized] private string _joinCode = "";

    [NonSerialized] public string gameId = "";
    [NonSerialized] public string serverId = "";
    [NonSerialized] public string organizationId = "";
	[NonSerialized] public bool isShutdownEventTriggered = false;
	[NonSerialized] public bool isAgonesShutdownTriggered = false;

    public ServerContext serverContext;

    private GameServer gameServer;

    [NonSerialized] public bool isServerReady = false;
    public event Action OnStartLoadingGame;
    public event Action OnServerReady;
    public event Action OnStartupConfigReady;
    public bool isStartupConfigReady = false;

    public event Action onProcessExit;

    private void Awake() {
        isServerReady = false;

#if UNITY_EDITOR && !AIRSHIP_PLAYER
	    var gameConfig = GameConfig.Load();
	    gameId = gameConfig.gameId;
	    serverContext.gameId = gameConfig.gameId;
#endif
    }

	private void Start() {
		if (!RunCore.IsServer()) {
			return;
		}

		if (!RunCore.IsClient()) {
			Application.targetFrameRate = 90;
		}


#if AIRSHIP_PLAYER
            #if AIRSHIP_STAGING
            Debug.Log("Server starting with STAGING configuration.");
            #else
            Debug.Log("Server starting with PRODUCTION configuration.");
            #endif
#endif

		EasyFileService.ClearCache();

		if (RunCore.IsEditor()) {
			ushort port = 7770;
#if UNITY_EDITOR
			port = AirshipEditorNetworkConfig.instance.portOverride;
#endif
			var transportOrLatencySim = AirshipNetworkManager.singleton.transport;
			if (transportOrLatencySim is LatencySimulation latencySim) {
				transportOrLatencySim = latencySim.wrap as KcpTransport;
			}

			if (transportOrLatencySim is not KcpTransport transport) {
				Debug.LogError("Transport is not of type KcpTransport.");
				return;
			}
			
			transport.port = port;

			if (RunCore.IsClient()) {
				// use random port in shared mode
				transport.port = (ushort)Random.Range(7770, 7870);
				// print("Listening on port " + transport.port);
				AirshipNetworkManager.singleton.StartHost();
			} else {
				AirshipNetworkManager.singleton.StartServer();
				Application.logMessageReceived += AnalyticsRecorder.RecordLogMessageToAnalytics;
			}
		} else {
			var transport = AirshipNetworkManager.singleton.transport as KcpTransport;
			transport.port = 7654;

			AirshipNetworkManager.singleton.StartServer();
			Application.logMessageReceived += AnalyticsRecorder.RecordLogMessageToAnalytics;
		}

		this.Setup();

		AppDomain.CurrentDomain.ProcessExit += ProcessExit;
	}

	public void InvokeOnProcessExit() {
		if (this.isShutdownEventTriggered) return;
		this.isShutdownEventTriggered = true;

		if ((this.onProcessExit?.GetInvocationList().Length ?? 0) > 0) {
			Debug.Log("Invoking OnProcessExit handlers.");
			this.onProcessExit?.Invoke();
		} else {
			Debug.LogWarning("No OnProcessExit handlers were registered. Directly exiting process.");
			this.Shutdown();
		}
	}

	private void OnDestroy() {
		AppDomain.CurrentDomain.ProcessExit -= ProcessExit;
	}

	private void ProcessExit(object sender, EventArgs args) {
		this.InvokeOnProcessExit();
	}

	public bool IsAgonesEnvironment() {
		return Environment.GetEnvironmentVariable("AGONES_SDK_HTTP_PORT") != null;
	}

	[HideFromTS][LuauAPI(LuauContext.Protected)]
	public GameServer GetGameServer() {
		return this.gameServer;
	}

	private async void Setup() {
		if (this.IsAgonesEnvironment() && RunCore.IsServer()) {
			var success = await agones.Connect();
			if (!success) {
				Debug.LogError("Failed to connect to Agones SDK server.");
				return;
			}
		}

		startupConfig = new StartupConfig() {
			GameBundleId = overrideGameBundleId,
			GameAssetVersion = overrideGameBundleVersion,
			packages = new(),
			CdnUrl = AirshipPlatformUrl.gameCdn,
		};

#if UNITY_EDITOR
		var gameConfig = GameConfig.Load();
		var editorStartingSceneIntent = EditorSessionState.GetString("AirshipEditorStartingSceneName");
		if (!string.IsNullOrEmpty(editorStartingSceneIntent)) {
			startupConfig.StartingSceneName = editorStartingSceneIntent;
		} else {
			startupConfig.StartingSceneName = gameConfig.startingScene.name;
		}
#endif

		if (this.IsAgonesEnvironment()) {
			/*
			 * This means we are on a real, remote server.
			 */

			// Wait for queue configuration to hit agones.
			this.gameServer = await agones.GameServer();
			OnGameServerChange(gameServer);

			agones.WatchGameServer(OnGameServerChange);

			await agones.Ready();
		} else {
			/*
			 * This means we are in local development.
			 */
#if UNITY_EDITOR
			this.startupConfig.packages = new();
			foreach (var package in gameConfig.packages) {
				this.startupConfig.packages.Add(package);
			}
#endif
			this.startupConfig.packages.Add(new AirshipPackageDocument() {
				id = this.startupConfig.GameBundleId,
				assetVersion = this.startupConfig.GameAssetVersion,
				codeVersion = this.startupConfig.GameCodeVersion,
				publishVersionNumber = this.startupConfig.GamePublishVersion,
				game = true
			});
			AnalyticsRecorder.InitGame(this.startupConfig);

			// remember, this is being called in local dev env. NOT STAGING!
			StartCoroutine(LoadWithStartupConfig(null, null));
		}
	}

	/**
     * Called whenever we receive GameServer changes from Agones.
     */
	private bool processedMarkedForDeletion = false;
	public void OnGameServerChange(GameServer server) {
		if (server == null) {
			Debug.Log("Agones GameServer is null. Ignoring.");
			return;
		}
		this.gameServer = server;

		if (!processedMarkedForDeletion && server.ObjectMeta != null && server.ObjectMeta.Labels != null && server.ObjectMeta.Labels.ContainsKey("MarkedForShutdown")) {
			Debug.Log("Found \"MarkedForShutdown\" label!");
			this.processedMarkedForDeletion = true;
			this.InvokeOnProcessExit();
		}
		
		if (this.allocatedByAgones) return;
		var annotations = server.ObjectMeta.Annotations;
		if (annotations.ContainsKey("GameId") && annotations.ContainsKey("JWT") && annotations.ContainsKey("RequiredPackages")) {
			Debug.Log($"[Agones]: Server will run game {annotations["GameId"]} with (Assets v{annotations["GameAssetVersion"]}) and (Code v{annotations["GameCodeVersion"]})");
			this.allocatedByAgones = true;
			startupConfig.GameBundleId = annotations["GameId"];
			startupConfig.GameAssetVersion = annotations["GameAssetVersion"];
			startupConfig.GameCodeVersion = annotations["GameCodeVersion"];
			startupConfig.GamePublishVersion = annotations["GamePublishVersion"];

			// print("required packages: " + annotations["RequiredPackages"]);
			var packagesString = "{\"packages\":" + annotations["RequiredPackages"] + "}";
 			var requiredPackages = JsonUtility.FromJson<RequiredPackagesDto>(packagesString);
            Debug.Log("RequiredPackages: " + packagesString);
            this.startupConfig.packages.Clear();
			foreach (var requiredPkg in requiredPackages.packages) {
				if (requiredPkg.assetVersionNumber <= 0) continue;
				this.startupConfig.packages.Add(new AirshipPackageDocument() {
					id = requiredPkg.packageSlug,
					assetVersion = requiredPkg.assetVersionNumber + "",
					codeVersion = requiredPkg.codeVersionNumber + "",
					publishVersionNumber = requiredPkg.publishVersionNumber + "",
					defaultPackage = true,
				});
			}

			startupConfig.StartingSceneName = annotations["GameSceneId"];
			if (annotations.TryGetValue("ShareCode", out var joinCode)) {
				this._joinCode = joinCode;
			}
			this.airshipJWT = annotations["JWT"];
			UnityWebRequestProxyHelper.ProxyAuthCredentials = this.airshipJWT;
			// Debug.Log("Airship JWT:");
			// Debug.Log(airshipJWT);

			this.gameId = annotations["GameId"];
			string gameCodeZipUrl = annotations[this.gameId + "_code"];
			// print("gameCodeZipUrl: " + gameCodeZipUrl);
			this.serverContext.gameId = this.gameId;
			if (annotations.TryGetValue("ServerId", out var serverId)) {
				this.serverId = serverId;
				this.serverContext.serverId = this.serverId;
				// Debug.Log("ServerId: " + serverId);
			} else {
				Debug.LogError("ServerId not found.");
			}

			this.organizationId = annotations["OrganizationId"];
			this.serverContext.organizationId = this.organizationId;

			var urlAnnotations = new string[] {
				"resources",
				"scenes",
			};

			var privateRemoteBundleFiles = new List<RemoteBundleFile>();

			// Download game's private server bundles
			foreach (var annotation in urlAnnotations) {
				var url = annotations[$"{startupConfig.GameBundleId}_{annotation}"];
				var fileName = $"server/{annotation}"; // IE. resources, resources.manifest, etc

				// Debug.Log($"Adding private remote bundle file. bundleId: {startupConfig.GameAssetVersion}, annotation: {annotation}, url: {url}");

				privateRemoteBundleFiles.Add(new RemoteBundleFile(
					fileName,
					url,
					startupConfig.GameBundleId,
					startupConfig.GameAssetVersion
				));
			}

			StartCoroutine(LoadRemoteGameId(privateRemoteBundleFiles, gameCodeZipUrl));
		}
	}

	/**
	 * Called after Agones annotations are loaded.
	 */
	private IEnumerator LoadRemoteGameId(List<RemoteBundleFile> privateRemoteBundleFiles, [CanBeNull] string gameCodeZipUrl) {
		OnStartLoadingGame?.Invoke();
		// StartupConfig is safe to use in here.

		// Download game config
		var url = $"{startupConfig.CdnUrl}/game/{startupConfig.GameBundleId}/code/{startupConfig.GameCodeVersion}/gameConfig.json";
		var request = UnityWebRequestProxyHelper.ApplyProxySettings(new UnityWebRequest(url));
		var gameConfigPath = Path.Combine(Application.persistentDataPath, "Games", startupConfig.GameBundleId, "gameConfig.json");
		request.downloadHandler = new DownloadHandlerFile(gameConfigPath);
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success) {
			Debug.LogError($"Failed to download gameConfig.json. url={url}, message={request.error}");
			Debug.Log("Retrying in 1s...");

			// Retry
			yield return new WaitForSecondsRealtime(1);
			yield return LoadRemoteGameId(privateRemoteBundleFiles, gameCodeZipUrl);
			yield break;
		}

		using var sr = new StreamReader(gameConfigPath);
		var jsonString = sr.ReadToEnd();
		var gameConfig = JsonUtility.FromJson<GameConfigDto>(jsonString);

		foreach (var package in gameConfig.packages) {
			// Ignore packages already in the startup config. Anything already in startup config is a "required package" at this point.
			// The below code is finding an existing package in startup config.
			if (this.startupConfig.packages.Find((p) => p.id.ToLower() == package.id.ToLower()) != null) {
				continue;
			}

			package.game = false;
			startupConfig.packages.Add(package);

			if (package.forceLatestVersion) {
				// latest version lookup
				// print("Fetching latest version of " + package.id);
				var res = InternalHttpManager.GetAsync(
					$"{AirshipPlatformUrl.deploymentService}/package-versions/packageSlug/{package.id}");
				yield return new WaitUntil(() => res.IsCompleted);
				// print("request complete.");
				if (res.Result.success) {
					var data = JsonUtility.FromJson<PackageLatestVersionResponse>(res.Result.data);
					try {
						package.codeVersion = data.version.package.codeVersionNumber.ToString();
						package.assetVersion = data.version.package.assetVersionNumber.ToString();
						package.publishVersionNumber = data.version.package.publishNumber.ToString();
					} catch (Exception e) {
						Debug.LogError("Failed to fetch latest version of " + package.id + ": " + e);
					}

					// Debug.Log("Fetched latest version of package " + package.id + " (Code v" + package.codeVersion + ", Assets v" + package.assetVersion + ")");
					// Debug.Log(res.Result.data);
				} else {
					// Debug.LogError("Failed to fetch latest version of package " + package.id + " " + res.Result.error);
				}
			}
		}


		this.startupConfig.packages.Add(new AirshipPackageDocument() {
			id = this.startupConfig.GameBundleId,
			assetVersion = this.startupConfig.GameAssetVersion,
			codeVersion = this.startupConfig.GameCodeVersion,
			publishVersionNumber = this.startupConfig.GamePublishVersion,
			game = true,
		});

		Debug.Log("Startup packages:");
		foreach (var doc in this.startupConfig.packages) {
			Debug.Log($"	 - id={doc.id}, version={doc.assetVersion}, code-version={doc.codeVersion}, publish={doc.publishVersionNumber}, game={doc.game},");
		}
		Debug.Log("  - " + gameCodeZipUrl);
		AnalyticsRecorder.InitGame(this.startupConfig);

		yield return LoadWithStartupConfig(privateRemoteBundleFiles.ToArray(), gameCodeZipUrl);
	}

	/**
     * Called once we have loaded all of StartupConfig from Agones & other sources.
     */
	private IEnumerator LoadWithStartupConfig(RemoteBundleFile[] privateBundleFiles, [CanBeNull] string gameCodeZipUrl) {
		List<AirshipPackage> packages = new();
		// StartupConfig will pull its packages from gameConfig.json
		foreach (var doc in startupConfig.packages) {
			// print("Loading pkg: " + doc.id);
			if (doc.id.ToLower() == "@easy/corematerials") continue;
			packages.Add(new AirshipPackage(doc.id, doc.assetVersion, doc.codeVersion, doc.publishVersionNumber, doc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
		}

		// Download bundles over network
		bool downloadComplete = false;
		if (!RunCore.IsEditor()) {
			BundleDownloader.Instance.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), privateBundleFiles, null, gameCodeZipUrl, false,
				(res) => {
					downloadComplete = true;
				});
		} else {
			downloadComplete = true;
		}

		while (!downloadComplete) {
			yield return null;
		}

		// print("[Airship]: Loading packages...");
        var stPackage = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles());
#if AIRSHIP_DEBUG
        print("Loaded packages in " + stPackage.ElapsedMilliseconds + " ms.");
#endif

        //Setup project configurations from loaded package
        PhysicsSetup.SetupFromGameConfig();

		this.isStartupConfigReady = true;
		this.OnStartupConfigReady?.Invoke();

		var clientBundleLoader = FindAnyObjectByType<ClientBundleLoader>(FindObjectsInactive.Include);
		clientBundleLoader.GenerateScriptsDto();
		// clientBundleLoader.LoadAllClients(startupConfig);

        var st = Stopwatch.StartNew();

        var scenePath = AirshipNetworkManager.GetAssetBundleScenePathFromName(startupConfig.StartingSceneName);
        AirshipNetworkManager.singleton.ServerChangeScene(scenePath);
        // SceneManager.LoadScene(scenePath, LoadSceneMode.Additive);
        yield return null;
        // if (!Application.isEditor) {
	       //  print("Loaded scenes:");
	       //  for (int i = 0; i < SceneManager.sceneCount; i++) {
		      //   print("  - " + SceneManager.GetSceneAt(i).name);
	       //  }
        // }
        // SceneManager.SetActiveScene(SceneManager.GetSceneByName(startupConfig.StartingSceneName));

        if (st.ElapsedMilliseconds > 100) {
	        Debug.Log("[Airship]: Finished loading server scene in " + st.ElapsedMilliseconds + "ms.");
        }

        isServerReady = true;
        OnServerReady?.Invoke();
	}

	public void Shutdown() {
		if (agones && !this.isAgonesShutdownTriggered) {
			this.isAgonesShutdownTriggered = true;
			agones.Shutdown();
			Application.Quit();
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

	public string GetJoinCode()
	{
		return _joinCode;
	}
}