using System;
using Editor.EditorInternal;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Airship.Editor {
    public class PrefabCustomImporter : AssetPostprocessor {
        private void OnPostprocessPrefab(GameObject g) {
            var behaviours = g.GetComponentsInChildren<AirshipComponent>();
            foreach (var behaviour in behaviours) {
                var artifactPath = AssetDatabase.GetAssetPath(behaviour.script);
                context.DependsOnArtifact(artifactPath);
                context.DependsOnSourceAsset(artifactPath);
                Debug.Log($"{context.assetPath} <-> {behaviour.script.assetPath}", behaviour.script);
            }
        }
        
        private void OnPreprocessAsset() {
            if (!assetImporter.IsPrefabImporter()) return;
            Debug.Log($"Importing prefab {context.assetPath}");
        }
    }
}