using System.Collections.Generic;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityScene = UnityEngine.SceneManagement.Scene;
using System.Collections;
using FishNet.Managing.Scened;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class SceneAsyncLoadEntry {
    public AsyncOperation ao;
    public string sceneName;

    public SceneAsyncLoadEntry(AsyncOperation ao, string sceneName) {
        this.ao = ao;
        this.sceneName = sceneName;
    }
}

public class EasySceneProcessor : SceneProcessorBase
{
    #region Private.
    /// <summary>
    /// Currently active loading AsyncOperations.
    /// </summary>
    protected List<SceneAsyncLoadEntry> LoadingAsyncOperations = new List<SceneAsyncLoadEntry>();
    /// <summary>
    /// A collection of scenes used both for loading and unloading.
    /// </summary>
    protected List<UnityScene> Scenes = new List<UnityScene>();
    /// <summary>
    /// Current AsyncOperation being processed.
    /// </summary>
    protected AsyncOperation CurrentAsyncOperation;
    #endregion

    private Scene fallbackScene;

    /// <summary>
    /// Called when scene loading has begun.
    /// </summary>
    public override void LoadStart(LoadQueueData queueData)
    {
        base.LoadStart(queueData);
        ResetValues();
    }

    public override void LoadEnd(LoadQueueData queueData) {
        base.LoadEnd(queueData);
        ResetValues();

        // Force enable post processing on all cameras
        // var cameras = GameObject.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // foreach (var c in cameras) {
        //     print("Updating camera " + c.gameObject.name);
        //     var data = c.GetUniversalAdditionalCameraData();
        //     data.renderPostProcessing = true;
        // }
    }

    /// <summary>
    /// Resets values for a fresh load or unload.
    /// </summary>
    private void ResetValues()
    {
        CurrentAsyncOperation = null;
        LoadingAsyncOperations.Clear();
    }

    /// <summary>
    /// Called when scene unloading has begun within an unload operation.
    /// </summary>
    /// <param name="queueData"></param>
    public override void UnloadStart(UnloadQueueData queueData)
    {
        base.UnloadStart(queueData);
        Scenes.Clear();
    }

    /// <summary>
    /// Begin loading a scene using an async method.
    /// </summary>
    /// <param name="sceneName">Scene name to load.</param>
    public override void BeginLoadAsync(string sceneName, UnityEngine.SceneManagement.LoadSceneParameters parameters) {
        // print("[AirshipSceneProcessor]: loading scene " + sceneName);
        AsyncOperation ao = null;

        foreach (var loadedAssetBundle in SystemRoot.Instance.loadedAssetBundles.Values) {
            foreach (var scenePath in loadedAssetBundle.assetBundle.GetAllScenePaths()) {
                if (scenePath.ToLower().EndsWith(sceneName.ToLower() + ".unity")) {
                    // print("[AirshipSceneProcessor]: Found scene to load inside bundle " + loadedAssetBundle.assetBundle.name);
                    ao = UnitySceneManager.LoadSceneAsync(scenePath, parameters);
                    break;
                }
            }
        }

        if (ao == null) {
            ao = UnitySceneManager.LoadSceneAsync(sceneName, parameters);
        }

        LoadingAsyncOperations.Add(new SceneAsyncLoadEntry(ao, sceneName));
        CurrentAsyncOperation = ao;
        CurrentAsyncOperation.allowSceneActivation = false;
    }

    /// <summary>
    /// Begin unloading a scene using an async method.
    /// </summary>
    /// <param name="sceneName">Scene name to unload.</param>
    public override void BeginUnloadAsync(UnityScene scene)
    {
        CurrentAsyncOperation = UnitySceneManager.UnloadSceneAsync(scene);
    }

    /// <summary>
    /// Returns if a scene load or unload percent is done.
    /// </summary>
    /// <returns></returns>
    public override bool IsPercentComplete()
    {
        return (GetPercentComplete() >= 0.9f);
    }

    /// <summary>
    /// Returns the progress on the current scene load or unload.
    /// </summary>
    /// <returns></returns>
    public override float GetPercentComplete()
    {
        return (CurrentAsyncOperation == null) ? 1f : CurrentAsyncOperation.progress;
    }

    /// <summary>
    /// Adds a loaded scene.
    /// </summary>
    /// <param name="scene">Scene loaded.</param>
    public override void AddLoadedScene(UnityScene scene)
    {
        base.AddLoadedScene(scene);
        Scenes.Add(scene);
    }

    /// <summary>
    /// Returns scenes which were loaded during a load operation.
    /// </summary>
    public override List<UnityScene> GetLoadedScenes()
    {
        return Scenes;
    }

    /// <summary>
    /// Activates scenes which were loaded.
    /// </summary>
    public override void ActivateLoadedScenes() {
        foreach (var sceneLoad in LoadingAsyncOperations) {
            // print("ActivateLoadedScenes setting active scene to " + sceneLoad.sceneName);
            // UnitySceneManager.SetActiveScene(UnitySceneManager.GetSceneByName(sceneLoad.sceneName));
            sceneLoad.ao.allowSceneActivation = true;
        }
    }

    /// <summary>
    /// Returns if all asynchronized tasks are considered IsDone.
    /// </summary>
    /// <returns></returns>
    public override IEnumerator AsyncsIsDone()
    {
        bool notDone;
        do
        {
            notDone = false;
            foreach (var sceneLoad in LoadingAsyncOperations)
            {

                if (!sceneLoad.ao.isDone)
                {
                    notDone = true;
                    break;
                }
            }
            yield return null;
        } while (notDone);

        yield break;
    }
}