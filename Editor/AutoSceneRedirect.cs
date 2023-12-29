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
        if (state == PlayModeStateChange.ExitingPlayMode && !string.IsNullOrEmpty(prevStartingScene)) {
            var gameConfig = GameConfig.Load();
            gameConfig.startingSceneName = prevStartingScene;
            prevStartingScene = null;
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        } else if (state == PlayModeStateChange.ExitingEditMode) {
            var gameConfig = GameConfig.Load();
            prevStartingScene = gameConfig.startingSceneName;
            gameConfig.startingSceneName = SceneManager.GetActiveScene().name;
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }
    }
}