using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class AutoSceneRedirect {
    private static string prevStartingScene;

    static AutoSceneRedirect() {
        EditorSceneManager.activeSceneChangedInEditMode += EditorSceneManager_ActiveSceneChangedInEditMode;
        EditorApplication.playModeStateChanged += PlayModeStateChanged;
    }

    private static void EditorSceneManager_ActiveSceneChangedInEditMode(Scene oldScene, Scene newScene) {
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) return;

        var foundScene = Array.Find(gameConfig.gameScenes, (s) => {
            string pathToScene = AssetDatabase.GetAssetPath(s);
            return pathToScene == newScene.path;
        });

        if (foundScene || (gameConfig.startingScene != null && gameConfig.startingScene.name == newScene.name)) {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity");
        } else {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    private static void PlayModeStateChanged(PlayModeStateChange state) {
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