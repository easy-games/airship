
    using UnityEditor;

    public static class AirshipScriptableObjectsHandler {
        [InitializeOnLoadMethod]
        public static void Load() {
            EditorApplication.playModeStateChanged += change => {
                if (change == PlayModeStateChange.ExitingPlayMode) {
                    var scriptableObjects = AssetDatabase.FindAssets($"t:{typeof(AirshipScriptableObject)}");
                    foreach (var guid in scriptableObjects) {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var asset = AssetDatabase.LoadAssetAtPath<AirshipScriptableObject>(path);
                        asset.Unload();
                        asset.ReconcileMetadata(ReconcileSource.ComponentValidate);
                    }
                }
            };
        }
    }
