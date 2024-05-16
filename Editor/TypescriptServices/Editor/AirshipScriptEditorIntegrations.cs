#if UNITY_EDITOR
using Luau;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Airship.Editor {
    public static class AirshipScriptEditorIntegrations {
        [MenuItem("Assets/Create/Airship Script (TS)", false, 50)]
        private static void CreateNewTypescriptFile()
        {
            ProjectWindowUtil.CreateAssetWithContent(
                "Script.ts",
                string.Empty);
        }
        
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var target = EditorUtility.InstanceIDToObject(instanceID);
            
            switch (target) {
                case BinaryFile: {
                    var path = AssetDatabase.GetAssetPath(instanceID);
                    TypescriptProjectsService.OpenFileInEditor(path);
                    return true;
                }
                case DeclarationFile: {
                    var path = AssetDatabase.GetAssetPath(instanceID);
                    TypescriptProjectsService.OpenFileInEditor(path);
                    return true;   
                }
                default:
                    return false;
            }
        }
    }
}
#endif