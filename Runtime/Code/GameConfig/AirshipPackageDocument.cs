using System;
using System.IO;
using UnityEngine.Serialization;

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
            var path = Path.GetRelativePath(".", Path.Combine("Assets", "Bundles", document.id)); // the relative is just to fix the slash on Windows lol
        
            if (Directory.Exists(path)) {
                return path;
            }
        
            return null;
        }
    }
}