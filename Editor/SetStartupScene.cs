using System;
using System.Collections;
using Editor.Packages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor {
    public class EditorInit : AssetPostprocessor {
        private static GameConfig _gameConfig;

        /*
         * On first open of editor, loads the first scene in your GameConfig.
         */
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            if (!didDomainReload) return;
            if (SessionState.GetBool("FirstSceneOpenDone", false)) return;

            _gameConfig = GameConfig.Load();
            EditorApplication.update += OpenSceneOncePackagesDownloaded;
        }

        private static void OpenSceneOncePackagesDownloaded() {
            // Wait for initial download of core packages
            if (AirshipPackageAutoUpdater.RequiresPackageDownloads(_gameConfig)) {
                return;
            }
            EditorApplication.update -= OpenSceneOncePackagesDownloaded;
            
            if (_gameConfig.gameScenes.Length > 0) {
                var sceneAsset = _gameConfig.gameScenes[0] as SceneAsset;
                var path = AssetDatabase.GetAssetPath(sceneAsset);
                EditorSceneManager.OpenScene(path);
                SessionState.SetBool("FirstSceneOpenDone", true);
                return;
            }

            foreach (var sceneInfo in EditorBuildSettings.scenes) {
                if (!sceneInfo.path.ToLower().StartsWith("assets")) {
                    continue;
                }
                EditorSceneManager.OpenScene(sceneInfo.path);
            }
            SessionState.SetBool("FirstSceneOpenDone", true);
        }
    }
}