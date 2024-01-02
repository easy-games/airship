using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReferenceBuilder {
    [CustomEditor(typeof(ReferenceBuilderAsset))]
    public class ReferenceBuilderAssetEditor : UnityEditor.Editor {
        private const string referenceAssetFolderPath = "Assets/Editor/Resources/ReferenceBuilderAssets";
        
        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Show default inspector property editor
            DrawDefaultInspector();

            GUIStyle textStyle = EditorStyles.label;
            textStyle.wordWrap = true;
            GUILayout.Label("References are compiled into Typescript. They will then need to be built to capture any changes. Make sure npm run watch is running or use the Build Game button after compiling.", textStyle);
            if (GUILayout.Button("Compile All References")) {
                ReferenceBuilderSerializer.Compile();
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("Airship/Misc/Reference Builder/Select Reference Assets")]
        public static void SelectReferences() {
            SelectFolder(referenceAssetFolderPath);
        }
        
        [MenuItem("Airship/Misc/Reference Builder/Create New Reference Assets")]
        public static void CreateReference() {
            ReferenceBuilderAsset asset = ScriptableObject.CreateInstance<ReferenceBuilderAsset>();

            AssetDatabase.CreateAsset(asset, Path.Combine(referenceAssetFolderPath, "ReferenceBuilder_.asset"));
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Airship/Misc/Reference Builder/Compile All References")]
        public static void CompileAllReferences() {
            ReferenceBuilderSerializer.Compile();
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
