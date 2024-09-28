using System;
using Editor.Packages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class AutoSceneRedirect {
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
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) return;

        bool sceneExistsInGameConfig = Array.Find(gameConfig.gameScenes, (s) => {
            string pathToScene = AssetDatabase.GetAssetPath(s);
            return pathToScene == scene.path;
        });

        if (sceneExistsInGameConfig || (gameConfig.startingScene != null && gameConfig.startingScene.name == scene.name)) {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity");
        } else {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    private static void PlayModeStateChanged(PlayModeStateChange state) {
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
            ServerBootstrap.editorStartingSceneIntent = sceneName;
        } else {
            ServerBootstrap.editorStartingSceneIntent = "";
        }
    }
}