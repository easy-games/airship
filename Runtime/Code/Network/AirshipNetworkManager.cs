using System;
using Assets.Luau.Network;
using Code.RemoteConsole;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AirshipNetworkManager : NetworkManager {
    public Net net;
    public ServerConsole serverConsole;

    public override void OnStartServer() {
        this.net.OnStartServer();
        this.serverConsole.OnStartServer();
    }

    public override void OnStartClient() {
        this.net.OnStartClient();
    }

    public override void OnClientConnect() {
        base.OnClientConnect();
        this.serverConsole.OnClientConnectedToServer();
    }

    private string GetAssetBundleScenePathFromName(string sceneName) {
        foreach (var loadedAssetBundle in SystemRoot.Instance.loadedAssetBundles.Values) {
            foreach (var scenePath in loadedAssetBundle.assetBundle.GetAllScenePaths()) {
                if (scenePath.ToLower().EndsWith(sceneName.ToLower() + ".unity")) {
                    return scenePath;
                }
            }
        }

        return sceneName;
    }

    public override async void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) {
        if (!customHandling) return;

        newSceneName = this.GetAssetBundleScenePathFromName(newSceneName);

        switch (sceneOperation) {
            case SceneOperation.Normal:
                loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName);
                break;
            case SceneOperation.LoadAdditive:
                // Ensure additive scene is not already loaded on client by name or path
                // since we don't know which was passed in the Scene message
                if (!SceneManager.GetSceneByName(newSceneName).IsValid() &&
                    !SceneManager.GetSceneByPath(newSceneName).IsValid()) {
                    loadingSceneAsync = SceneManager.LoadSceneAsync(newSceneName, LoadSceneMode.Additive);
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
                            if (LuauCore.IsProtectedScene(s.name)) continue;
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
                    loadingSceneAsync = SceneManager.UnloadSceneAsync(newSceneName,
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