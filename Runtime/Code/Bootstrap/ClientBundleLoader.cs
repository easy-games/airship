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
    public EasyEditorConfig editorConfig;

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

    private void ServerBootstrap_OnServerReady()
    {
        foreach (var conn in connectionsToLoad)
        {
            LoadConnection(conn);
        }
    }

    private void SceneManager_OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    }

    private IEnumerator ClientSetup(StartupConfig startupConfig) {
        Debug.Log("Starting client setup. Packages: " + startupConfig.packages.Count);

        List<AirshipPackage> packages = new();
        foreach (var packageDoc in startupConfig.packages) {
            packages.Add(new AirshipPackage(packageDoc.id, packageDoc.version, packageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
        }

        if (CrossSceneState.IsLocalServer() || CrossSceneState.UseLocalBundles)
        {
            Debug.Log("Skipping bundle download.");
        } else {
            var bundleDownloader = GameObject.FindObjectOfType<BundleDownloader>();
            yield return bundleDownloader.DownloadBundles(startupConfig.CdnUrl, packages.ToArray());
        }

        Debug.Log("Starting to load game: " + startupConfig.GameBundleId);
        yield return SystemRoot.Instance.LoadBundles(startupConfig.GameBundleId, editorConfig, packages);

        Debug.Log("Finished loading bundles. Requesting scene load...");
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
        var sceneName = this.serverBootstrap.startupConfig.StartingSceneName;
        var sceneLoadData = new SceneLoadData(sceneName);
        sceneLoadData.ReplaceScenes = ReplaceOption.None;
        sceneLoadData.Options = new LoadOptions()
        {
            AutomaticallyUnload = false,
        };
        InstanceFinder.SceneManager.LoadConnectionScenes(connection, sceneLoadData);
    }

    [ServerRpc(RequireOwnership = false)]
    public void UnloadGameSceneServerRpc(NetworkConnection conn = null)
    {
        var sceneUnloadData = new SceneUnloadData(new string[] {"CoreScene", this.serverBootstrap.startupConfig.StartingSceneName});
        sceneUnloadData.Options.Mode = UnloadOptions.ServerUnloadMode.KeepUnused;
        InstanceFinder.SceneManager.UnloadConnectionScenes(conn, sceneUnloadData);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DisconnectServerRpc(NetworkConnection conn = null)
    {
        if (conn != null) {
            conn.Disconnect(true);
        }
    }
    
}