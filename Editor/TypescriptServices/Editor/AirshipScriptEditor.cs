using System.IO;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Airship.Editor {
#if UNITY_EDITOR
    [CustomEditor(typeof(BinaryFile))]
    public class BinaryFileEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var project = TypescriptProjectsService.Project;
            
            var script = (BinaryFile)target;
            
            GUI.enabled = true;
            
            // EditorGUILayout.LabelField("TS Path", self.path);
            // EditorGUILayout.LabelField("Luau Path", TypescriptImporter.ProjectConfig.GetOutputPath(self.path));
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Refresh")) {
                    AssetDatabase.StartAssetEditing();
                    AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                    AssetDatabase.StopAssetEditing();
                }
                
                if (GUILayout.Button("Edit File")) {
                    TypescriptProjectsService.OpenFileInEditor(script.m_path);
                }

                if (script.scriptLanguage == AirshipScriptLanguage.Typescript) {
                    GUI.enabled = script.m_compiled;
                    if (GUILayout.Button("View Luau Output"))
                    {
                        TypescriptProjectsService.OpenFileInEditor(project.GetOutputPath(script.assetPath));
                    }                   
                }
                
 
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = false;
        }
    }

    
    public static class ScriptedAssetOpenFileHandler {
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
                default:
                    return false;
            }
        }
    }
#endif
}