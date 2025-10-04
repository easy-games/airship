using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Base class to derive custom property drawers from.
/// </summary>
public abstract class AirshipEditor : ScriptableObject {
    internal AirshipSerializedObject _serializedObject;
    protected AirshipSerializedObject serializedObject => _serializedObject;
    
    protected void DrawDefault() {
        foreach (var property in _serializedObject.GetProperties()) {
            AirshipEditorGUI.PropertyField(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
        }
    }
    
    public virtual void OnInspectorGUI() {
        this.DrawDefault();
    }
}