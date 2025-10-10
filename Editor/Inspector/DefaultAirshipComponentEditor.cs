

using Luau;
using UnityEditor;
using UnityEngine;

public class DefaultAirshipComponentEditor : AirshipEditor {
    private void DrawDefaultScriptHeader() {
        EditorGUILayout.Space(5);
        var scriptPath = script.assetPath;

        GUI.enabled = false;
        var newScript = EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);
        GUI.enabled = true;
        
        EditorGUILayout.Space(5);
    }

    private void DrawProperties() {
        // Draw each property
        foreach (var property in serializedObject.GetProperties()) {
            AirshipEditorGUI.PropertyField(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
        }
    }

    private void DrawInternalDebug() {
        if (Application.isPlaying) {
            var binding = (AirshipComponent)target;
            if (binding == null) return;
            
            AirshipEditorGUI.HorizontalLine();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("GameObject Id", AirshipBehaviourRootV2.GetId(binding.gameObject).ToString());
                EditorGUILayout.LabelField("Component Id", binding.GetAirshipComponentId().ToString());
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Context", binding.context.ToString());
            EditorGUILayout.EndHorizontal();
        }        
    }
    
    public override void OnInspectorGUI() {
        DrawDefaultScriptHeader();
        DrawProperties();
        
        #if AIRSHIP_INTERNAL
            DrawInternalDebug();
        #endif
    }
}
