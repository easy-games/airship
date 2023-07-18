using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Bootstrap
{
    public class ClientBundleUnloader : Singleton<ClientBundleUnloader>
    {
        private void Awake()
        {
            DontDestroyOnLoad(this);
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
            // Debug.Log("OnSceneUnloaded: " + scene.name);
            // if (scene.name == "CoreScene")
            // {
            //     var systemRoot = FindObjectOfType<SystemRoot>();
            //     systemRoot.UnloadBundles();
            // }
        }
    }
}