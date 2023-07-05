using System;
using FishNet;
using UnityEngine;

public class ClientSceneListener : MonoBehaviour
{
    public bool IsGameSceneLoaded = false;
    public delegate void SceneLoadedEvent(string sceneName);
    public event SceneLoadedEvent sceneLoadedEvent;

    public float SceneLoadPercent = 0;
    public delegate void SceneLoadPercentChanged(float percent);
    public event SceneLoadPercentChanged sceneLoadPercentChanged;

    private CoreLoadingScreen _coreLoadingScreen;

    private void Awake()
    {
        _coreLoadingScreen = FindObjectOfType<CoreLoadingScreen>();
        InstanceFinder.SceneManager.OnLoadEnd += (e) =>
        {
            foreach (var loadedScene in e.LoadedScenes)
            {
                // Debug.Log("Scene loaded: " + loadedScene.name);
                //
                // Debug.Log(
                //     "Objects:");
                // foreach (var obj in GameObject.FindObjectsOfType<MonoBehaviour>())
                // {
                //     print(obj.name);
                // }
                // Debug.Log("----- end");
                
                
                IsGameSceneLoaded = true;
                sceneLoadedEvent?.Invoke(loadedScene.name);
            }
        };

        InstanceFinder.SceneManager.OnLoadPercentChange += e =>
        {
            print("Load percent: " + e.Percent);
            SceneLoadPercent = e.Percent;
            sceneLoadPercentChanged?.Invoke(e.Percent);
            _coreLoadingScreen.SetProgress("Loading Files (" + (e.Percent * 100).ToString("#") + ")", 0);
        };
    }
    
}