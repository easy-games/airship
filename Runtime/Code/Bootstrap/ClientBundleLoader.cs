using System;
using System.Collections;
using System.Collections.Generic;
using Code.Bootstrap;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientBundleLoader : NetworkBehaviour {
    private ServerBootstrap serverBootstrap;
    private List<NetworkConnection> connectionsToLoad = new();
    public AirshipEditorConfig editorConfig;

    private string expectingGameSceneToLoad = null;

    private void Awake()
    {
        if (RunCore.IsClient())
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
        } else {
            this.serverBootstrap = FindObjectOfType<ServerBootstrap>();
        }
    }

    private void OnDestroy()
    {
        if (RunCore.IsClient())
        {
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

        var serverBootstrap = FindObjectOfType<ServerBootstrap>();
        serverBootstrap.OnServerReady += ServerBootstrap_OnServerReady;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        var serverBootstrap = FindObjectOfType<ServerBootstrap>();
        if (serverBootstrap)
        {
            serverBootstrap.OnServerReady -= ServerBootstrap_OnServerReady;
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
            Debug.Log("Skipping bundle download.");
        } else {
            var loadingScreen = FindObjectOfType<CoreLoadingScreen>();
            var bundleDownloader = GameObject.FindObjectOfType<BundleDownloader>();
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
        var serverBootstrap = FindObjectOfType<ServerBootstrap>();
        if (!serverBootstrap.serverReady)
        {
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