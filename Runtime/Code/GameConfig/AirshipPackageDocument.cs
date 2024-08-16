using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Code.GameBundle {

    [Serializable]
    public class AirshipPackageDocument {
        public string id;
        [FormerlySerializedAs("version")] public string assetVersion;
        public string codeVersion;
        public bool game = false;
        public bool localSource = false;
        public bool disabled = false;
        public bool defaultPackage = false;
        public bool forceLatestVersion = false;

        public static string FindPathFromDocument(AirshipPackageDocument document) {
            var path = Path.GetRelativePath(".", Path.Combine("Assets", "AirshipPackages", document.id)); // the relative is just to fix the slash on Windows
        
            if (Directory.Exists(path)) {
                return path;
            }
        
            return null;
        }

        public bool IsDownloaded() {
            var path = Path.GetRelativePath(".", Path.Combine("Assets", "AirshipPackages", this.id)); // the relative is just to fix the slash on Windows
            if (!Directory.Exists(path)) {
                return false;
            }

            var filePath = Path.Join(path, "airship_pkg_download_success.txt");
            
            // In editor a package is downloaded when it exists in asset database
            #if UNITY_EDITOR
            if (!AssetDatabase.LoadAssetAtPath<Object>(filePath)) {
                return false;
            }
            #endif
            if (!File.Exists(filePath)) {
                return false;
            }

            return true;
        }
    }
}