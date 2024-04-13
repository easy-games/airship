using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace Code.Airship.Resources.Scripts.Editor {
    public class AirshipAssetPresets {
        private static string presetsPath = "Packages/gg.easy.airship/Runtime/Presets";
        public class EnforcePresetPostProcessor : AssetPostprocessor {
            void OnPreprocessAsset() {
                var firstImport = assetImporter.importSettingsMissing;
                if (!firstImport || AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null) {
                    return;
                }
                
                if (!AssetDatabase.IsValidFolder(assetPath) && !assetPath.EndsWith(".cs") &&
                    !assetPath.EndsWith(".preset")) {
                    if (!AssetDatabase.IsValidFolder(presetsPath)) return;

                    var presetPaths = Directory.EnumerateFiles(presetsPath, "*.preset", SearchOption.AllDirectories);
                    foreach (var path in presetPaths) {
                        var preset = AssetDatabase.LoadAssetAtPath<Preset>(path);
                        if (preset == null) continue;

                        preset.ApplyTo(assetImporter);
                    }
                }
            }
        }
    }
}