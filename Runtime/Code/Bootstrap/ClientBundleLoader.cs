using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Code.Bootstrap;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class ClientBundleLoader : NetworkBehaviour {
    [SerializeField]
    public ServerBootstrap serverBootstrap;
    private List<NetworkConnection> connectionsToLoad = new();
    public AirshipEditorConfig editorConfig;

    private BinaryFile binaryFileTemplate;

    public Stopwatch codeReceiveSt = new Stopwatch();

    private void Awake() {
        if (RunCore.IsClient()) {
            this.binaryFileTemplate = ScriptableObject.CreateInstance<BinaryFile>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
        }
    }

    private void OnDestroy() {
        if (RunCore.IsClient()) {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneManager_OnSceneLoaded;
        }
    }

    public override void OnSpawnServer(NetworkConnection connection) {
        base.OnSpawnServer(connection);
        if (this.serverBootstrap.isStartupConfigReady) {
            this.SetupConnection(connection, this.serverBootstrap.startupConfig);
        }
    }

    [Server]
    private void SetupConnection(NetworkConnection connection, StartupConfig startupConfig) {
        // print("Setting up connection " + connection.ClientId);

        var root = SystemRoot.Instance;
        int pkgI = 0;
        foreach (var pair1 in root.luauFiles) {
            int fileI = 0;
            foreach (var filePair in pair1.Value) {
                var bf = filePair.Value;
                var metadataJson = string.Empty;
                if (bf.m_metadata != null) {
                    metadataJson = JsonUtility.ToJson(bf.m_metadata);
                }
                this.SendLuaBytes(connection, pair1.Key, filePair.Key, bf.m_bytes, metadataJson,  pkgI == 0 && fileI == 0,  pkgI == root.luauFiles.Count - 1 && fileI == pair1.Value.Count - 1);
                fileI++;
            }

            pkgI++;
        }

        this.LoadGameRpc(connection, startupConfig);
    }

    [TargetRpc]
    public void SendLuaBytes(NetworkConnection conn, string packageKey, string path, byte[] bytes, string metadataText, bool firstMessage, bool finalMessage) {
        if (firstMessage) {
            this.codeReceiveSt.Restart();
        }
        LuauMetadata metadata = null;
        if (!string.IsNullOrEmpty(metadataText)) {
            var (m, s) = LuauMetadata.FromJson(metadataText);
            metadata = m;
        }

        var br = Object.Instantiate(this.binaryFileTemplate);
        br.m_bytes = bytes;
        br.m_path = path;
        br.m_metadata = metadata;

        var split = path.Split("/");
        if (split.Length > 0) {
            br.name = split[split.Length - 1];
        }

        var root = SystemRoot.Instance;
        root.AddLuauFile(packageKey, br);

        if (finalMessage) {
            Debug.Log("Received code in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");
        }
    }

    [TargetRpc][ObserversRpc]
    public void LoadGameRpc(NetworkConnection conn, StartupConfig startupConfig) {
        StartCoroutine(this.ClientSetup(startupConfig));
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        this.serverBootstrap.OnServerReady += ServerBootstrap_OnServerReady;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (this.serverBootstrap) {
            this.serverBootstrap.OnServerReady -= ServerBootstrap_OnServerReady;
        }
    }

    private void ServerBootstrap_OnServerReady() {
        foreach (var conn in connectionsToLoad) {
            LoadConnection(conn);
        }
    }

    private void SceneManager_OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.IsValid()) {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        }
    }

    private IEnumerator ClientSetup(StartupConfig startupConfig) {
        if (!RunCore.IsClient()) {
            yield break;
        }

        List<AirshipPackage> packages = new();
        foreach (var packageDoc in startupConfig.packages) {
            packages.Add(new AirshipPackage(packageDoc.id, packageDoc.assetVersion, packageDoc.codeVersion, packageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
        }

        if (CrossSceneState.IsLocalServer() || CrossSceneState.UseLocalBundles)
        {
            // Debug.Log("Skipping bundle download.");
        } else {
            var loadingScreen = FindAnyObjectByType<CoreLoadingScreen>();
            var bundleDownloader = FindAnyObjectByType<BundleDownloader>();
            yield return bundleDownloader.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), null, loadingScreen);
        }

        // Debug.Log("Starting to load game: " + startupConfig.GameBundleId);
        if (!RunCore.IsServer()) {
            yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles(editorConfig), false);
        }

        EasyFileService.ClearCache();

        // Debug.Log("Finished loading bundles. Requesting scene load...");
        LoadGameSceneServerRpc();
    }

    [Server]
    public void LoadAllClients(StartupConfig config) {
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values) {
            this.SetupConnection(conn, config);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadGameSceneServerRpc(NetworkConnection conn = null)
    {
        if (!this.serverBootstrap.serverReady) {
            connectionsToLoad.Add(conn);
            return;
        }

        LoadConnection(conn);
    }

    [Server]
    private void LoadConnection(NetworkConnection connection)
    {
        var sceneName = this.serverBootstrap.startupConfig.StartingSceneName.ToLower();
        var scenePath = $"assets/bundles/shared/scenes/{sceneName}.unity";
        var sceneLoadData = new SceneLoadData(scenePath);
        sceneLoadData.ReplaceScenes = ReplaceOption.None;
        sceneLoadData.Options = new LoadOptions()
        {
            AutomaticallyUnload = false,
        };
        InstanceFinder.SceneManager.LoadConnectionScenes(connection, sceneLoadData);
    }

}