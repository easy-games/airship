

using Luau;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The default editor for airship components
/// </summary>
public class DefaultAirshipComponentEditor : AirshipEditor {
    public override void OnInspectorGUI() {
        // By default we just draw the default inspector here.
        DrawDefaultInspector();
    }
}

/// <summary>
/// The default editor for airship components
/// </summary>
public class DefaultAirshipScriptableObjectEditor : AirshipEditor {
    private void OnScriptSelectionChanged(AirshipScript targetScript) {
        unitySerializedObject.FindProperty("script").objectReferenceValue = targetScript;
        unitySerializedObject.ApplyModifiedProperties();
        unitySerializedObject.Update();
    }
    
    public override void OnInspectorGUI() {
        EditorGUILayout.Space(5);

        if (script != null) {
            GUI.enabled = script == null;
            EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), false);
            GUI.enabled = true;
        } else {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            AirshipEditorGUI.AirshipScriptField(rect, new GUIContent("Script"), script, OnScriptSelectionChanged, AirshipEditorGUI.ScriptExportType.ScriptableObject);
        }
        
        EditorGUILayout.Space(5);
        
        DrawDefaultProperties();
    }
}

public class DefaultAirshipSerializableObjectEditor : AirshipEditor {
    public override void OnInspectorGUI() {
        EditorGUILayout.HelpBox("Serializable Class Objects are not yet supported at Runtime", MessageType.Error);
        DrawDefaultProperties();
    }
}