
    using UnityEditor;

    public static class AirshipScriptableObjectsHandler {
        [InitializeOnLoadMethod]
        public static void Load() {
            EditorApplication.playModeStateChanged += change => {
                if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredPlayMode) {
                    var scriptableObjects = AssetDatabase.FindAssets($"t:{typeof(AirshipScriptableObject)}");
                    foreach (var guid in scriptableObjects) {
                        var path = AssetDatabase.GUIDToAssetPath(guid);

                        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in assets) {
                            if (asset is not AirshipScriptableObject scriptableObjectAsset) continue;
                            scriptableObjectAsset.Unload();
                            scriptableObjectAsset.ReconcileMetadata(ReconcileSource.ComponentValidate);
                        }
                        
                        // var asset = AssetDatabase.LoadAssetAtPath<AirshipScriptableObject>(path);
                        // asset.Unload();
                        // asset.ReconcileMetadata(ReconcileSource.ComponentValidate);
                    }
                }
            };
        }
    }
