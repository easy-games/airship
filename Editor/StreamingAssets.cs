using System;
using System.IO;
using Code.Bootstrap;
using Editor;
using Editor.Util;
using UnityEditor;
using UnityEngine;

#if AIRSHIP_PLAYER
internal class StreamingAssets {
    private static readonly string ShippedBundles = "Assets/StreamingAssets/ShippedBundles";
    private static readonly string ShippedBundlesTmp = "Assets/StreamingAssets/ShippedBundles~";

    public static readonly string CoreMaterialsAndroid = "Assets/StreamingAssets/ShippedBundles/CoreMaterials_Android";
    public static readonly string CoreMaterialsWindows = "Assets/StreamingAssets/ShippedBundles/CoreMaterials_Windows";
    public static readonly string CoreMaterialsIOS = "Assets/StreamingAssets/ShippedBundles/CoreMaterials_iOS";
    public static readonly string CoreMaterialsMac = "Assets/StreamingAssets/ShippedBundles/CoreMaterials_Mac";

    public static readonly string RelativeResourcesPath = "@easy/corematerials_shared/resources";
    
    private static void ExcludeStreamingAssetsPath(string path) {
        var targetPath = path.Replace("StreamingAssets", "StreamingAssets~");
        
        if (!Directory.Exists(targetPath)) {
            Directory.CreateDirectory(targetPath);
        }
        Directory.Move(path, targetPath);
    }

    private static void FixStreamingAssetsPath(string sourcePath) {
        var resourcePath = PosixPath.Join(sourcePath, RelativeResourcesPath);
        var excludedResourcePath = resourcePath.Replace("StreamingAssets", "StreamingAssets~");
        if (!File.Exists(excludedResourcePath)) {
            Debug.LogWarning($"Could not find {excludedResourcePath}");
            return;
        }
        File.Move(excludedResourcePath, resourcePath);
        AssetDatabase.ImportAsset(resourcePath);
    }
    
    public static void SetCoreMaterialPlatform(AirshipPlatform platform) {
        var platformPath = platform switch {
            AirshipPlatform.Android => CoreMaterialsAndroid,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        var paths = new[] {
            CoreMaterialsAndroid,
            CoreMaterialsMac,
            CoreMaterialsWindows,
            CoreMaterialsIOS,
        };

        foreach (var path in paths) {
            if (path == platformPath) continue;
            var resourcePath = PosixPath.Join(path, RelativeResourcesPath);
            if (!File.Exists(resourcePath)) {
                Debug.LogWarning($"could not find resource path {resourcePath}");
                continue;
            }


            var targetPath = resourcePath.Replace("StreamingAssets", "StreamingAssets~");
            var targetFolder = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(targetFolder)) {
                Directory.CreateDirectory(targetFolder);
            }
            
            File.Move(resourcePath, targetPath);
        }
        
        AssetDatabase.ImportAsset("Assets/StreamingAssets", ImportAssetOptions.ImportRecursive);
    }
    
    public static void ResetCoreMaterials() {
        FixStreamingAssetsPath(CoreMaterialsAndroid);
        FixStreamingAssetsPath(CoreMaterialsWindows);
        FixStreamingAssetsPath(CoreMaterialsMac);
        FixStreamingAssetsPath(CoreMaterialsIOS);
    }
}
#endif
