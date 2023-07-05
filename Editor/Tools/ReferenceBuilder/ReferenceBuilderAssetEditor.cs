using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReferenceBuilder {
    [CustomEditor(typeof(ReferenceBuilderAsset))]
    public class ReferenceBuilderAssetEditor : UnityEditor.Editor {
        private const string referenceAssetFolderPath = "Assets/Game/BedWars/Editor/Resources/ReferenceBuilderAssets";
        
        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Show default inspector property editor
            DrawDefaultInspector();

            if (GUILayout.Button("Compile")) {
                ReferenceBuilderSerializer.Compile();
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("EasyGG/Reference Builder/Select Reference Assets")]
        public static void SelectReferences() {
            SelectFolder(referenceAssetFolderPath);
        }
        
        [MenuItem("EasyGG/Reference Builder/Create New Reference Assets")]
        public static void CreateReference() {
            ReferenceBuilderAsset asset = ScriptableObject.CreateInstance<ReferenceBuilderAsset>();

            AssetDatabase.CreateAsset(asset, Path.Combine(referenceAssetFolderPath, "ReferenceBuilder_.asset"));
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void SelectFolder(string path) {
            // Check the path has no '/' at the end, if it dose remove it,
            if (path[path.Length -1] == '/')
                path = path.Substring(0, path.Length -1);
 
            // Load object
            Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
 
            // Select the object in the project folder
            Selection.activeObject = obj;
 
            // Also flash the folder yellow to highlight it
            EditorGUIUtility.PingObject(obj);
        }
    }
    
}
