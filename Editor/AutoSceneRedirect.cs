using System;
using Code.State;
using Editor.Packages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class AutoSceneRedirect {
    public static bool disableSceneRedirect = false;
    public static Func<bool> shouldRedirectToCoreCallback;

    static AutoSceneRedirect() {
        EditorSceneManager.activeSceneChangedInEditMode += EditorSceneManager_ActiveSceneChangedInEditMode;
        EditorApplication.playModeStateChanged += PlayModeStateChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OnReload() {
        HandleRedirectBasedOnActiveScene(SceneManager.GetActiveScene());
    }

    private static void EditorSceneManager_ActiveSceneChangedInEditMode(Scene oldScene, Scene newScene) {
        HandleRedirectBasedOnActiveScene(newScene);
    }

    private static void HandleRedirectBasedOnActiveScene(Scene scene) {
        #if AIRSHIP_PLAYER
        return;
        #endif
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) return;

        bool isForced = false;
        if (shouldRedirectToCoreCallback != null) {
            isForced = shouldRedirectToCoreCallback();
        }

        bool sceneExistsInGameConfig = false;
        if (!isForced) {
            sceneExistsInGameConfig = Array.Find(gameConfig.gameScenes, (s) => {
                string pathToScene = AssetDatabase.GetAssetPath(s);
                return pathToScene == scene.path;
            });
        }

        if (isForced ||  sceneExistsInGameConfig || (gameConfig.startingScene != null && gameConfig.startingScene.name == scene.name)) {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity");
        } else {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    private static void PlayModeStateChanged(PlayModeStateChange state) {
        if (disableSceneRedirect) return;
#if AIRSHIP_PLAYER
        return;
#endif

        if (state == PlayModeStateChange.EnteredPlayMode) {
            if (AirshipPackageAutoUpdater.isCoreUpdateAvailable) {
                Debug.Log("An Airship Core update is available. Not updating may result in unexpected behaviour.");
            }
        }
        if (state != PlayModeStateChange.ExitingEditMode) return;
        
        var sceneName = SceneManager.GetActiveScene().name;
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) return;

        if (Array.Find(gameConfig.gameScenes, obj => ((SceneAsset)obj).name == sceneName) != null) {
            EditorSessionState.SetString("AirshipEditorStartingSceneName", sceneName);
        } else {
            EditorSessionState.SetString("AirshipEditorStartingSceneName", "");
        }
    }
}