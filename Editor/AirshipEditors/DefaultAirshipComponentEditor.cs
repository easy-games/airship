

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
    public override void OnInspectorGUI() {
        EditorGUILayout.HelpBox("Scriptable Objects are experimental and subject to change", MessageType.Warning);
        DrawDefaultProperties();
    }
}

public class DefaultAirshipSerializableObjectEditor : AirshipEditor {
    public override void OnInspectorGUI() {
        EditorGUILayout.HelpBox("Serializable Class Objects are not yet supported at Runtime", MessageType.Error);
        DrawDefaultProperties();
    }
}