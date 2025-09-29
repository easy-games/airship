#if UNITY_EDITOR
using System.IO;
using Editor.Util;
using Luau;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Airship.Editor {
    public static class AirshipScriptEditorIntegrations {
        internal static string TemplatePath => "Packages/gg.easy.airship/Editor/Templates";
        internal static string AirshipComponentTemplate => PosixPath.Join(TemplatePath, "AirshipComponent.ts.txt");
        internal static string AirshipModuleTemplate => PosixPath.Join(TemplatePath, "AirshipModule.ts.txt");

        [MenuItem("Assets/Create/Airship/TypeScript File", false, 50)]
        private static void CreateNewComponentFile()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(AirshipComponentTemplate, "AirshipComponent.ts");
        }
        
        // [MenuItem("Assets/Create/Airship Script", false, 51)]
        // private static void CreateNewScriptFile()
        // {
        //     ProjectWindowUtil.CreateScriptAssetFromTemplateFile(AirshipModuleTemplate, "Script.ts");
        // }
        
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var target = EditorUtility.InstanceIDToObject(instanceID);
            
            switch (target) {
                case AirshipScript: {
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