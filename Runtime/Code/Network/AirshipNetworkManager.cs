using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AirshipNetworkManager : NetworkManager {
    public override async void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) {
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
                    Debug.LogWarning($"Scene {newSceneName} is already loaded");

                    // Reset the flag that we disabled before entering this switch
                    NetworkClient.isLoadingScene = false;
                }
                break;
            case SceneOperation.UnloadAdditive:
                // Ensure additive scene is actually loaded on client by name or path
                // since we don't know which was passed in the Scene message
                if (SceneManager.GetSceneByName(newSceneName).IsValid() ||
                    SceneManager.GetSceneByPath(newSceneName).IsValid()) {
                    loadingSceneAsync = SceneManager.UnloadSceneAsync(newSceneName,
                        UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                } else {
                    Debug.LogWarning($"Cannot unload {newSceneName} with UnloadAdditive operation");

                    // Reset the flag that we disabled before entering this switch
                    NetworkClient.isLoadingScene = false;
                }
                break;
        }
    }
}