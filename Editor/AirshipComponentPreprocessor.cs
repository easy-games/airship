using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor {
    public class AirshipComponentPreprocessor : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            if (assetPath.EndsWith(".prefab")) {
                AddCustomDependencies();
            }
        }

        private void AddCustomDependencies() {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset == null) return;
            
            AirshipComponent[] components = prefabAsset.GetComponentsInChildren<AirshipComponent>(true);
            foreach (AirshipComponent component in components) {
                if (!string.IsNullOrEmpty(component.m_fileFullPath)) {
                    context.DependsOnArtifact(component.m_fileFullPath);
                }
            }
        }
    }
}