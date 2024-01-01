using System;
using System.Collections;
using System.Collections.Generic;
using Code.Bootstrap;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class ClientBundleLoader : NetworkBehaviour {
    [SerializeField]
    public ServerBootstrap serverBootstrap;
    private List<NetworkConnection> connectionsToLoad = new();
    public AirshipEditorConfig editorConfig;

    private void Awake() {
        if (RunCore.IsClient()) {
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
            this.LoadGameRpc(connection, this.serverBootstrap.startupConfig);
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
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    }

    private IEnumerator ClientSetup(StartupConfig startupConfig) {
        List<AirshipPackage> packages = new();
        foreach (var packageDoc in startupConfig.packages) {
            packages.Add(new AirshipPackage(packageDoc.id, packageDoc.version, packageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
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
        yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles(editorConfig));

        // Debug.Log("Finished loading bundles. Requesting scene load...");
        LoadGameSceneServerRpc();
    }

    [Server]
    public void LoadAllClients(StartupConfig config)
    {
        LoadGameRpc(null, config);
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadGameSceneServerRpc(NetworkConnection conn = null)
    {
        print("serverRpc 1");
        if (!this.serverBootstrap.serverReady) {
            Debug.Log("Adding connection to join queue.");
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