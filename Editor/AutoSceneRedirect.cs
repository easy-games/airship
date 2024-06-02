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

        if (foundScene || gameConfig.startingSceneName == newScene.name) {
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

        Debug.Log("Scene intent: " + sceneName);
        if (Array.Find(gameConfig.gameScenes, obj => ((SceneAsset)obj).name == sceneName) != null) {
            ServerBootstrap.editorStartingSceneIntent = sceneName;
        } else {
            ServerBootstrap.editorStartingSceneIntent = "";
        }
        /*
         * This code changes the GameConfig's starting scene to whatever is active when clicking play.
         * This saves you from having to change GameConfig when wanting to play different scenes.
         */
        // if (state == PlayModeStateChange.ExitingPlayMode && !string.IsNullOrEmpty(prevStartingScene)) {
        //     if (prevStartingScene == "CoreScene") return;
        //     Debug.Log($"Prev starting scene: \"{prevStartingScene}\"");
        //
        //     var gameConfig = GameConfig.Load();
        //     gameConfig.startingSceneName = prevStartingScene;
        //     prevStartingScene = null;
        //     EditorUtility.SetDirty(gameConfig);
        //     AssetDatabase.Refresh();
        //     AssetDatabase.SaveAssets();
        // } else if (state == PlayModeStateChange.ExitingEditMode) {
        //     var activeScene = SceneManager.GetActiveScene().name;
        //     if (activeScene == "CoreScene") return;
        //
        //     var gameConfig = GameConfig.Load();
        //     prevStartingScene = gameConfig.startingSceneName;
        //     gameConfig.startingSceneName = activeScene;
        //     EditorUtility.SetDirty(gameConfig);
        //     AssetDatabase.Refresh();
        //     AssetDatabase.SaveAssets();
        // }
    }
}