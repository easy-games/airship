using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Luau.Network;
using Code.Bootstrap;
using Code.RemoteConsole;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AirshipNetworkManager : NetworkManager {
    public Net net;
    public ServerConsole serverConsole;
    public ClientBundleLoader clientBundleLoader;

    public override void OnStartServer() {
        this.net.OnStartServer();
        this.serverConsole.OnStartServer();
        this.clientBundleLoader.SetupServer();
    }

    public void LogDedicated(string msg) {
#if UNITY_SERVER
        Debug.Log(msg);
#endif
    }

    public override void OnStartClient() {
        this.net.OnStartClient();
        this.clientBundleLoader.SetupClient();
    }

    public override void OnClientConnect() {
        base.OnClientConnect();
        this.serverConsole.OnClientConnectedToServer();

        var clientNetworkConnector = FindAnyObjectByType<ClientNetworkConnector>();
        if (clientNetworkConnector != null) {
            clientNetworkConnector.reconnectAttempt = 0;
            clientNetworkConnector.NetworkClient_OnConnected();
        }
    }

    public override void OnStopClient() {
        base.OnStopClient();
        // Debug.Log("OnStopClient");
        this.clientBundleLoader.CleanupClient();

        var clientNetworkConnector = FindAnyObjectByType<ClientNetworkConnector>();
        if (clientNetworkConnector != null) {
            clientNetworkConnector.NetworkClient_OnDisconnected();
        }
    }

    public override void OnStopServer() {
        base.OnStopServer();
        this.clientBundleLoader.CleanupServer();
    }

    public override void ServerChangeScene(string newSceneName) {
        if (string.IsNullOrWhiteSpace(newSceneName))
        {
            Debug.LogError("ServerChangeScene empty scene name");
            return;
        }

        if (NetworkServer.isLoadingScene && newSceneName == networkSceneName)
        {
            Debug.LogError($"Scene change is already in progress for {newSceneName}");
            return;
        }

        // Throw error if called from client
        // Allow changing scene while stopping the server
        if (!NetworkServer.active && newSceneName != offlineScene)
        {
            Debug.LogError("ServerChangeScene can only be called on an active server.");
            return;
        }

        var scenePath = GetAssetBundleScenePathFromName(newSceneName);

        // Debug.Log($"ServerChangeScene {newSceneName}");
        NetworkServer.SetAllClientsNotReady();
        networkSceneName = newSceneName;

        // Let server prepare for scene change
        OnServerChangeScene(newSceneName);

        // set server flag to stop processing messages while changing scenes
        // it will be re-enabled in FinishLoadScene.
        NetworkServer.isLoadingScene = true;

        loadingSceneAsync = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
        SetSceneActiveOnceLoaded(loadingSceneAsync, newSceneName);

        // ServerChangeScene can be called when stopping the server
        // when this happens the server is not active so does not need to tell clients about the change
        if (NetworkServer.active)
        {
            // notify all clients about the new scene
            // NetworkServer.SendToAll(new SceneMessage
            // {
            //     sceneName = newSceneName,
            //     sceneOperation = SceneOperation.LoadAdditive,
            //     customHandling = true,
            // });
        }

        startPositionIndex = 0;
        startPositions.Clear();
    }

    private async Task SetSceneActiveOnceLoaded(AsyncOperation ao, string sceneName) {
        await ao;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded) {
            SceneManager.SetActiveScene(scene);
        }
    }

    public override async void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) {
        if (!customHandling) return;

        var scenePath = GetAssetBundleScenePathFromName(newSceneName);

        while (!this.clientBundleLoader.isFinishedPreparing) {
            // print($"waiting for prepare finish. packages={this.clientBundleLoader.packagesReady}, scripts={this.clientBundleLoader.scriptsReady}");
            await Awaitable.NextFrameAsync();

            if (SceneManager.GetActiveScene().name != "CoreScene") {
                // cancelled
                // print("cancelled");
                return;
            }
        }

        switch (sceneOperation) {
            case SceneOperation.Normal:
                loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);
                break;
            case SceneOperation.LoadAdditive:
                // Ensure additive scene is not already loaded on client by name or path
                // since we don't know which was passed in the Scene message
                if (!SceneManager.GetSceneByName(newSceneName).IsValid() &&
                    !SceneManager.GetSceneByPath(newSceneName).IsValid()) {
                    loadingSceneAsync = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                    await loadingSceneAsync;
                    var loadedScene = SceneManager.GetSceneByName(newSceneName);
                    if (loadedScene.IsValid()) {
                        SceneManager.SetActiveScene(loadedScene);
                    }
                } else {
                    // Debug.LogWarning($"Scene {newSceneName} is already loaded");

                    // Reset the flag that we disabled before entering this switch
                    NetworkClient.isLoadingScene = false;
                }
                break;
            case SceneOperation.UnloadAdditive:
                // Ensure additive scene is actually loaded on client by name or path
                // since we don't know which was passed in the Scene message
                if (SceneManager.GetSceneByName(newSceneName).IsValid() ||
                    SceneManager.GetSceneByPath(newSceneName).IsValid()) {
                    bool isActiveScene = SceneManager.GetActiveScene().name == newSceneName;
                    bool foundNewActiveScene = false;
                    Scene newActiveScene = default;
                    if (isActiveScene) {
                        for (int i = 0; i < SceneManager.sceneCount; i++) {
                            var s = SceneManager.GetSceneAt(i);
                            if (LuauCore.IsProtectedScene(s)) continue;
                            if (s.name == newSceneName) continue;
                            foundNewActiveScene = true;
                            newActiveScene = s;
                            break;
                        }
                    }

                    if (isActiveScene) {
                        if (!foundNewActiveScene) {
                            throw new Exception(
                                "Can't unload scene because no scene was found to replace as active scene.");
                        }
                        SceneManager.SetActiveScene(newActiveScene);
                    }
                    loadingSceneAsync = SceneManager.UnloadSceneAsync(scenePath,
                        UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    await loadingSceneAsync;
                    if (isActiveScene) {
                        SceneManager.SetActiveScene(newActiveScene);
                    }
                } else {
                    Debug.LogWarning($"Cannot unload {newSceneName} with UnloadAdditive operation");

                    // Reset the flag that we disabled before entering this switch
                    NetworkClient.isLoadingScene = false;
                }
                break;
        }
    }
}