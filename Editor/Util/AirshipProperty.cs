using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEngine;

public abstract class AirshipEditor : ScriptableObject {
    internal SerializedObject serializedObject;
    internal SerializedProperty serializedMetadata;
    internal SerializedProperty serializedName;
    internal SerializedProperty serializedProperties;

    [CanBeNull]
    public AirshipProperty FindProperty(string targetPropertyName) {
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            var propertyName = property.FindPropertyRelative("name").stringValue;
            if (propertyName == targetPropertyName) {
                return new AirshipProperty(property);
            }
        }

        return default;
    }

    public AirshipProperty[] GetProperties() {
        var properties = new AirshipProperty[serializedProperties.arraySize];
        
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            // var propertyName = property.FindPropertyRelative("name").stringValue;
            properties[i] = new AirshipProperty(property);
        }

        return properties;
    }

    public void DrawDefault() {
        foreach (var property in GetProperties()) {
            AirshipEditorGUI.PropertyFieldLayout(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
        }

        serializedObject.ApplyModifiedProperties();
    }
    
    public virtual void OnInspectorGUI() {
    }
}

public class AirshipCustomEditorAttribute : Attribute {
    public string FilePath { get; }

    public AirshipCustomEditorAttribute(string filePath) {
        FilePath = filePath;
    }
}


public static class AirshipCustomEditors {
    private static Dictionary<string, Type> editorTypes = new();
    private static Dictionary<int, AirshipEditor> editors = new();
    
    [InitializeOnLoadMethod]
    internal static void RegisterCustomEditors() {
        var editors = TypeCache.GetTypesWithAttribute<AirshipCustomEditorAttribute>();
        foreach (var editor in editors) {
            var attr = editor.GetCustomAttributes<AirshipCustomEditorAttribute>();
            foreach (var editorAttribute in attr) {
                if (!AirshipCustomEditors.editorTypes.TryGetValue(editorAttribute.FilePath, out var _)) {
                    Debug.Log($"Register custom editor {editorAttribute.FilePath} ??");
                    AirshipCustomEditors.editorTypes.Add(editorAttribute.FilePath, editor);
                }
            }
        }
    }

    public static Type GetEditorForFilePath(string filePath) {
        Debug.Log($"Try getting registered path {filePath}");
        if (editorTypes.TryGetValue(filePath, out var editorType)) {
            Debug.Log($"Got registered path {filePath} {editorType.Name}");
            return editorType;
        }

        return null;
    }

    public static AirshipEditor GetEditor(AirshipComponent component, Type type, SerializedObject serializedObject) {
        if (editors.TryGetValue(component.GetInstanceID(), out var editor)) {
            editor.serializedObject = serializedObject;
            editor.serializedMetadata = serializedObject.FindProperty("metadata");
            editor.serializedName = serializedObject.FindProperty("name");
            editor.serializedProperties = editor.serializedMetadata.FindPropertyRelative("properties");
            return editor;
        }

        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        editor.serializedObject = serializedObject;
        editor.serializedMetadata = serializedObject.FindProperty("metadata");
        editor.serializedName = serializedObject.FindProperty("name");
        editor.serializedProperties = editor.serializedMetadata.FindPropertyRelative("properties");
        
        editors.Add(component.GetInstanceID(), editor);
        
        Debug.Log($"Register editor instance for {component.GetInstanceID()} - {editor.serializedName.stringValue}");
        return editor;
    }
}

public struct AirshipProperty {
    internal SerializedProperty serializedName { get; }
    internal SerializedProperty serializedType { get; }
    internal SerializedProperty serializedObjectType { get; }
    internal SerializedProperty serializedValue { get; }
    internal SerializedProperty serializedObject { get; }
    internal SerializedProperty serializedModified { get; }
    internal SerializedProperty serializedItems { get; }
    internal SerializedProperty serializedDecorators { get; }
    internal SerializedProperty serializedRefPath { get; }
    
    public string name => serializedName.stringValue;
    public AirshipType airshipType => isAirshipType ? AirshipBuildInfo.Instance.GetTypeByName(serializedObjectType.stringValue) : null;
    public string type => serializedType.stringValue;
    public Type objectType => serializedObjectType.stringValue != "" ? TypeReflection.GetTypeFromString(serializedObjectType.stringValue) : typeof(UnityEngine.Object);
    public bool isModified => serializedModified.boolValue;
    public bool isObject => serializedType.stringValue == "object";
    public bool isArray => serializedType.stringValue == "Array";
    public bool isEnum => serializedType.stringValue is "IntEnum" or "StringEnum" or "FlagEnum";
    public bool isAirshipType => serializedType.stringValue == "AirshipBehaviour";
    public LuauMetadataDecoratorValue[] decorators => null;

    public UnityEngine.Object objectReferenceValue {
        get {
            return serializedObject.objectReferenceValue;
        }
        set {
            if (value != serializedObject.objectReferenceValue) serializedModified.boolValue = true;
            serializedObject.objectReferenceValue = value;
        }
    }

    public string stringValue {
        get {
            return serializedValue.stringValue;
        }
        set {
            if (type != "string") throw new InvalidCastException("Expected string");
            serializedValue.stringValue = value;
            serializedModified.boolValue = true;
        }
    }

    public bool boolValue {
        get {
            return serializedValue.stringValue == "1";
        }
        set {
            if (type != "boolean") throw new InvalidCastException("Expected string");
            serializedValue.stringValue = value ? "1" : "0";
        }
    }

    public AirshipArrayProperty<T> GetArray<T>() {
        return new AirshipArrayProperty<T>(this);
    }

    public void ResetToDefaultValue() {
        // TODO: Reset to default value (if applicable)
    }

    public AirshipProperty(SerializedProperty property) {
        serializedName = property.FindPropertyRelative("name");
        serializedType = property.FindPropertyRelative("type");
        serializedValue = property.FindPropertyRelative("serializedValue");
        serializedObject = property.FindPropertyRelative("serializedObject");
        serializedObjectType = property.FindPropertyRelative("objectType");
        serializedModified = property.FindPropertyRelative("modified");
        serializedItems = property.FindPropertyRelative("serializedItems");
        serializedDecorators = property.FindPropertyRelative("decorators");
        serializedRefPath = property.FindPropertyRelative("refPath");
    }

    public static bool operator ==(AirshipProperty lhs, AirshipProperty rhs) {
        return lhs.serializedName == rhs.serializedName;
    }
    
    public static bool operator !=(AirshipProperty lhs, AirshipProperty rhs) {
        return lhs.serializedName != rhs.serializedName;
    }
    
    public static implicit operator bool(AirshipProperty property) {
        return property.serializedName != default;
    }
}