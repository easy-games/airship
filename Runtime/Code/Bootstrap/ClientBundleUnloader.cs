using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientBundleUnloader : MonoBehaviour
{
    private SystemRoot _systemRoot;
    private void Awake()
    {
        DontDestroyOnLoad(this);
        _systemRoot = FindObjectOfType<SystemRoot>();

        if (RunCore.IsClient())
        {
            SceneManager.sceneUnloaded += SceneManager_OnSceneUnloaded;
        }
    }

    private void OnDestroy()
    {
        if (RunCore.IsClient())
        {
            SceneManager.sceneUnloaded -= SceneManager_OnSceneUnloaded;
        }
    }

    private void SceneManager_OnSceneUnloaded(Scene scene)
    {
        Debug.Log("OnSceneUnloaded: " + scene.name);
        if (scene.name == "CoreScene")
        {
            _systemRoot.UnloadBundles();
        }
    }
}