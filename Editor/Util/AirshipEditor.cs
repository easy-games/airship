using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Base class to derive custom property drawers from.
/// </summary>
public abstract class AirshipEditor : ScriptableObject {
    internal AirshipSerializedObject _serializedObject;
    protected AirshipSerializedObject serializedObject => _serializedObject;
    
    public AirshipProperty.Decorator[] decorators {  get; internal set; }
    
    protected void DrawDefault() {
        foreach (var property in _serializedObject.GetProperties()) {
            AirshipEditorGUI.PropertyField(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
        }
    }

    private void OnEnable() {
        
    }

    private void OnDisable() {
        Debug.Log($"Destruct obj {serializedObject.serializedName.stringValue}");
    }

    public bool PropertyField(string propertyName) {
        return AirshipEditorGUI.PropertyField(serializedObject.FindAirshipProperty(propertyName));
    }
    
    public virtual void OnInspectorGUI() {
        this.DrawDefault();
    }
}