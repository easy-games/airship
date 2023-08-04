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

public class ClientBundleLoader : NetworkBehaviour
{
    [Tooltip("Forces asset download in editor instead of local assets.")]
    public bool downloadBundles = false;
    [SyncVar][NonSerialized] private StartupConfig _startupConfig;
    private List<NetworkConnection> connectionsToLoad = new();

    public EasyEditorConfig editorConfig;

    private void Awake()
    {
        if (RunCore.IsClient())
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        if (RunCore.IsClient())
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneManager_OnSceneLoaded;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(ClientSetup());
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        var serverBootstrap = FindObjectOfType<ServerBootstrap>();
        serverBootstrap.onServerReady += ServerBootstrap_OnServerReady;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        var serverBootstrap = FindObjectOfType<ServerBootstrap>();
        if (serverBootstrap)
        {
            serverBootstrap.onServerReady -= ServerBootstrap_OnServerReady;
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

    private IEnumerator ClientSetup()
    {
        if (CrossSceneState.IsLocalServer() || CrossSceneState.UseLocalBundles)
        {
            Debug.Log("Skipping bundle download.");
        } else {
            var gameBundle = new AirshipBundle(_startupConfig.GameBundleId, _startupConfig.GameBundleVersion);
            var coreBundle = new AirshipBundle(_startupConfig.CoreBundleId, _startupConfig.CoreBundleVersion);

            var bundleDownloader = GameObject.FindObjectOfType<BundleDownloader>();
            yield return bundleDownloader.DownloadBundles(_startupConfig, new []{ gameBundle, coreBundle });
        }

        Debug.Log("Starting to load game: " + _startupConfig.GameBundleId);
        yield return SystemRoot.Instance.LoadBundles(_startupConfig.GameBundleId, editorConfig);

        Debug.Log("Finished loading bundles. Requesting scene load...");
        LoadGameSceneServerRpc();
    }

    public void SetStartupConfig(StartupConfig config)
    {
        _startupConfig = config;
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
        var sceneName = _startupConfig.StartingSceneName;
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
        var sceneUnloadData = new SceneUnloadData(new string[] {"CoreScene", _startupConfig.StartingSceneName});
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