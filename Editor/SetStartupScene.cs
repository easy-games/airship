using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor {
    public class EditorInit : AssetPostprocessor {

        /*
         * On first open of editor, loads the first scene in your GameConfig.
         */
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            if (!didDomainReload) return;
            if (SessionState.GetBool("FirstSceneOpenDone", false)) return;

            var gameConfig = GameConfig.Load();
            if (gameConfig.gameScenes.Length > 0) {
                var sceneAsset = gameConfig.gameScenes[0] as SceneAsset;
                var path = AssetDatabase.GetAssetPath(sceneAsset);
                EditorSceneManager.OpenScene(path);
                SessionState.SetBool("FirstSceneOpenDone", true);
                return;
            }

            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++) {
                var path = EditorBuildSettings.scenes[i];
                if (!path.path.ToLower().StartsWith("assets")) {
                    continue;
                }
                EditorSceneManager.OpenScene(path.path);
            }
            SessionState.SetBool("FirstSceneOpenDone", true);
        }
    }
}