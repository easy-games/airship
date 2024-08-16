using System.IO;
using System.Text.RegularExpressions;
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
            var prefabContent = File.ReadAllText(assetPath);
            var matches = Regex.Matches(prefabContent, @"m_fileFullPath: (.+)");

            foreach (Match match in matches) {
                if (match.Groups.Count > 1) {
                    // Groups[1] is the first capture group for this match
                    var fileFullPath = match.Groups[1].Value.Trim();
                    // Replace .lua -> .ts
                    fileFullPath = fileFullPath[..^".lua".Length] + ".ts";
                    if (!string.IsNullOrEmpty(fileFullPath)) {
                        context.DependsOnArtifact(fileFullPath);
                    }
                }
            }
        }
    }
}