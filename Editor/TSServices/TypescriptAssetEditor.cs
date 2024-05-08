using System.IO;
using Editor;
using Luau;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Airship.Editor {
#if UNITY_EDITOR
    [CustomEditor(typeof(TypescriptFile))]
    public class BinaryFileEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var self = (TypescriptFile) target;
            
            GUI.enabled = true;
            
            EditorGUILayout.LabelField("TS Path", self.path);
            EditorGUILayout.LabelField("Luau Path", TypescriptImporter.TypescriptConfig.GetOutputPath(self.path));
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Refresh")) {
                    AssetDatabase.StartAssetEditing();
                    AssetDatabase.ImportAsset(self.path, ImportAssetOptions.Default);
                    AssetDatabase.StopAssetEditing();
                }
                
                if (GUILayout.Button("Edit File")) {
                    TypescriptProjectsService.OpenFileInEditor(self.path);
                }

                GUI.enabled = self.binaryFile != null && self.binaryFile.m_compiled;
                if (GUILayout.Button("View Luau Output"))
                {
                    TypescriptProjectsService.OpenFileInEditor(self.binaryFile.m_path);
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
                    TypescriptProjectsService.OpenFileInEditor(TypescriptImporter.TypescriptConfig.GetOutputPath(path));
                    return true;
                }
                case TypescriptFile: {
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