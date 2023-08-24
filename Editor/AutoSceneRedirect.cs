using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class AutoSceneRedirect {
    static AutoSceneRedirect() {
        EditorSceneManager.activeSceneChangedInEditMode += EditorSceneManager_ActiveSceneChangedInEditMode;
    }

    private static void EditorSceneManager_ActiveSceneChangedInEditMode(Scene _, Scene newScene) {
        var gameConfig = GameConfig.Load();
        if (gameConfig == null) return;

        var foundScene = Array.Find(gameConfig.gameScenes, (s) => {
            string pathToScene = AssetDatabase.GetAssetPath(s);
            return pathToScene == newScene.path;
        });
        if (foundScene) {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity");
        } else {
            EditorSceneManager.playModeStartScene = null;
        }
    }
}