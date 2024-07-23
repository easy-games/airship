using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Agones;
using Agones.Model;
using Code.Bootstrap;
using Code.GameBundle;
using Code.Http.Internal;
using Code.Http.Public;
using Code.Platform.Shared;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using JetBrains.Annotations;
using Proyecto26;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

[Serializable]
public struct StartupConfig {
	public string GameBundleId; // bedwars but could be islands, etc
	[FormerlySerializedAs("GameBundleVersion")] public string GameAssetVersion; // UUID
	public string GameCodeVersion;
	public string StartingSceneName;
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

	private bool _launchedServer = false;

	[NonSerialized] private string _joinCode = "";

    [NonSerialized] public string gameId = "";
    [NonSerialized] public string serverId = "";
    [NonSerialized] public string organizationId = "";

    public ServerContext serverContext;

    /// <summary>
    /// When set, this will be used as the starting scene.
    /// </summary>
    public static string editorStartingSceneIntent;

    public bool serverReady = false;
    public event Action OnStartLoadingGame;
    public event Action OnServerReady;
    public event Action OnStartupConfigReady;
    public bool isStartupConfigReady = false;

    public event Action onProcessExit;

    private void Awake()
    {
        // if (RunCore.IsClient()) {
	       //  return;
        // }
        serverReady = false;

#if UNITY_EDITOR
	    var gameConfig = GameConfig.Load();
	    gameId = gameConfig.gameId;
	    serverContext.gameId.Value = gameConfig.gameId;
#endif

        Application.targetFrameRate = 90;

        SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
	}

	private void Start()
	{
		if (!RunCore.IsServer()) {
			return;
		}

		InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;

		if (RunCore.IsEditor())
		{
			ushort port = 7770;
			#if UNITY_EDITOR
			port = AirshipEditorNetworkConfig.instance.portOverride;
			#endif
			InstanceFinder.ServerManager.StartConnection(port);
		}
		else
		{
			InstanceFinder.ServerManager.StartConnection(7654);
		}

		AppDomain.CurrentDomain.ProcessExit += ProcessExit;
	}

	public void InvokeOnProcessExit() {
		this.onProcessExit?.Invoke();
	}

	private void OnDestroy() {
		AppDomain.CurrentDomain.ProcessExit -= ProcessExit;
	}

	private void ProcessExit(object sender, EventArgs args) {
		Debug.Log("----> Process Exit!");
		this.onProcessExit?.Invoke();
	}

	private void OnDisable()
	{
		// if (RunCore.IsClient()) return;

		SceneManager.sceneLoaded -= SceneManager_OnSceneLoaded;
		if (InstanceFinder.ServerManager)
		{
			InstanceFinder.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
		}
	}

	private void SceneManager_OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// SceneManager.SetActiveScene(scene);
	}

	public bool IsAgonesEnvironment()
	{
		return Environment.GetEnvironmentVariable("AGONES_SDK_HTTP_PORT") != null;
	}

	private async void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
	{
		if (args.ConnectionState == LocalConnectionState.Started) {
			// Server has bound to port.
			var loadData = new SceneLoadData("CoreScene");
			// loadData.PreferredActiveScene = new PreferredScene(new SceneLookupData("CoreScene"));
			InstanceFinder.SceneManager.LoadGlobalScenes(loadData);

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
				CdnUrl = "https://gcdn-staging.easy.gg",
			};

#if UNITY_EDITOR
			var gameConfig = GameConfig.Load();
			if (!string.IsNullOrEmpty(editorStartingSceneIntent)) {
				startupConfig.StartingSceneName = editorStartingSceneIntent;
			} else {
				startupConfig.StartingSceneName = gameConfig.startingSceneName;
			}
#endif

			if (this.IsAgonesEnvironment()) {
				/*
				 * This means we are on a real, remote server.
				 */

				// Wait for queue configuration to hit agones.

				var gameServer = await agones.GameServer();
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
					game = true
				});

				// remember, this is being called in local dev env. NOT STAGING!
				StartCoroutine(LoadWithStartupConfig(null, null));
			}
		}
	}

	/**
     * Called whenever we receive GameServer changes from Agones.
     */
	private bool processedMarkedForDeletion = false;
	private void OnGameServerChange(GameServer server) {
		if (!processedMarkedForDeletion && server.ObjectMeta.Labels.ContainsKey("MarkedForShutdown")) {
			Debug.Log("Found \"MarkedForShutdown\" label!");
			this.processedMarkedForDeletion = true;
			this.InvokeOnProcessExit();
		}
		
		if (_launchedServer) return;
		var annotations = server.ObjectMeta.Annotations;
		if (annotations.ContainsKey("GameId") && annotations.ContainsKey("JWT") && annotations.ContainsKey("RequiredPackages")) {
			Debug.Log($"[Agones]: Server will run game {annotations["GameId"]} with (Assets v{annotations["GameAssetVersion"]}) and (Code v{annotations["GameCodeVersion"]})");
			_launchedServer = true;
			startupConfig.GameBundleId = annotations["GameId"];
			startupConfig.GameAssetVersion = annotations["GameAssetVersion"];
			startupConfig.GameCodeVersion = annotations["GameCodeVersion"];

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
			this.serverContext.gameId.Value = this.gameId;
			if (annotations.TryGetValue("ServerId", out var serverId)) {
				this.serverId = serverId;
				this.serverContext.serverId.Value = this.serverId;
				// Debug.Log("ServerId: " + serverId);
			} else {
				Debug.LogError("ServerId not found.");
			}

			this.organizationId = annotations["OrganizationId"];
			this.serverContext.organizationId.Value = this.organizationId;

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
		var gameConfigPath = Path.Join(AssetBridge.GamesPath, startupConfig.GameBundleId, "gameConfig.json");
		request.downloadHandler = new DownloadHandlerFile(gameConfigPath);
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success) {
			Debug.LogError($"Failed to download gameConfig.json. url={url}, message={request.error}");
			Debug.Log("Retrying in 1s...");

			// Retry
			yield return new WaitForSeconds(1);
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
					$"{AirshipUrl.DeploymentService}/package-versions/packageSlug/{package.id}");
				yield return new WaitUntil(() => res.IsCompleted);
				// print("request complete.");
				if (res.Result.success) {
					var data = JsonUtility.FromJson<PackageLatestVersionResponse>(res.Result.data);
					package.codeVersion = data.package.codeVersionNumber.ToString();
					package.assetVersion = data.package.assetVersionNumber.ToString();
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
			game = true,
		});

		Debug.Log("Startup packages:");
		foreach (var doc in this.startupConfig.packages) {
			Debug.Log($"	 - id={doc.id}, version={doc.assetVersion}, code-version={doc.codeVersion}, game={doc.game},");
		}
		Debug.Log("  - " + gameCodeZipUrl);

		yield return LoadWithStartupConfig(privateRemoteBundleFiles.ToArray(), gameCodeZipUrl);
	}

	/**
     * Called once we have loaded all of StartupConfig from Agones & other sources.
     */
	[Server]
	private IEnumerator LoadWithStartupConfig(RemoteBundleFile[] privateBundleFiles, [CanBeNull] string gameCodeZipUrl) {
		List<AirshipPackage> packages = new();
		// StartupConfig will pull its packages from gameConfig.json
		foreach (var doc in startupConfig.packages) {
			packages.Add(new AirshipPackage(doc.id, doc.assetVersion, doc.codeVersion, doc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
		}

		// Download bundles over network
		var forceDownloadPackages = false;
		if (!RunCore.IsEditor() || forceDownloadPackages) {
			yield return BundleDownloader.Instance.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), privateBundleFiles, null, gameCodeZipUrl);
		}

		// print("[Airship]: Loading packages...");
        var stPackage = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles());
#if AIRSHIP_DEBUG
        print("Loaded packages in " + stPackage.ElapsedMilliseconds + " ms.");
#endif

		this.isStartupConfigReady = true;
		this.OnStartupConfigReady?.Invoke();

		var clientBundleLoader = FindAnyObjectByType<ClientBundleLoader>();
		clientBundleLoader.GenerateScriptsDto();
		clientBundleLoader.LoadAllClients(startupConfig);

        var st = Stopwatch.StartNew();
        var startupSceneLookup = new SceneLookupData(startupConfig.StartingSceneName);
        var sceneLoadData = new SceneLoadData(startupSceneLookup);
        sceneLoadData.PreferredActiveScene = new PreferredScene(startupSceneLookup);
        // Load scene on the server only
        InstanceFinder.SceneManager.LoadConnectionScenes(sceneLoadData);
        if (st.ElapsedMilliseconds > 100) {
	        Debug.Log("[Airship]: Finished loading scene in " + st.ElapsedMilliseconds + "ms.");
        }

        serverReady = true;
        OnServerReady?.Invoke();
    }

	public void Shutdown()
	{
		if (agones)
		{
			agones.Shutdown();
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