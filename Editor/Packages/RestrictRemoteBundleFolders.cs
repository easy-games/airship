using System.Collections.Generic;
using System.IO;
using Code.GameBundle;
using UnityEditor;
using UnityEngine;

namespace Editor.Packages {
     public class RestrictRemoteBundleFolders : AssetPostprocessor {
        private static GameConfig gameConfig = GameConfig.Load();
        private static List<string> toDelete = new();
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {
            return; // Disabled because download could complete prior to asset import (resulting in lots of incorrect spam errors)
            
            // Disabled in settings
            if (!EditorIntegrationsConfig.instance.safeguardBundleModification) return;
            // Disabled while building bundles (this triggers reimports)
            if (CreateAssetBundles.buildingBundles || AirshipPackagesWindow.buildingAssetBundles) return;
            // Disabled while downloading packages
            if (AirshipPackagesWindow.activeDownloads.Count > 0) return;
            
            foreach (var importedAsset in importedAssets) {
                var package = GetAssetPackage(importedAsset);
                if (package == null || package.localSource) continue;
                    
                
                if (AirshipPackagesWindow.activeDownloads.Contains(package.id)) {
                    continue;
                }
                
                var option = EditorUtility.DisplayDialogComplex("Modifying Restricted Folder",
                    $"You are adding an asset to a package folder. This will be overriden.\n\n{importedAsset}", "Undo",
                    "Ignore", "Don't show again");

                switch (option) {
                    case 0: // Ok
                        toDelete.Add(importedAsset);
                        EditorApplication.update += DeletePendingAssets;
                        break;
                    case 1: // Ignore
                        break;
                    case 2: // Don't show again
                        EditorIntegrationsConfig.instance.safeguardBundleModification = false;
                        return; // Stop this run of post processing
                }
            }
        }

        private static AirshipPackageDocument GetAssetPackage(string assetPath) {
            if (!assetPath.StartsWith("Assets/AirshipPackages/")) return null;
            assetPath = assetPath.Substring("Assets/AirshipPackages/".Length);
            
            foreach (var package in gameConfig.packages) {
                // Check if asset is part of a package
                if (assetPath.StartsWith(package.id + "/")) {
                    return package;
                }
            }
            return null;
        }

        private static void DeletePendingAssets() {
            foreach (var path in toDelete) {
                if (Directory.Exists(path)) {
                    Directory.Delete(path);
                    continue;
                }
                if (File.Exists(path)) {
                    File.Delete(path);
                    continue;
                }
            }
            AssetDatabase.Refresh();
            toDelete.Clear();
            EditorApplication.update -= DeletePendingAssets;
        }
    }
}