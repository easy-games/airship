using System;
using System.Collections.Generic;
using System.Reflection;
using Luau;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AirshipCustomEditors {
    private static Dictionary<string, Type> editorTypes = new();
    private static Dictionary<AirshipType, Type> airshipTypeToEditorTypes = new();
    
    private static Dictionary<int, AirshipEditor> editors = new();

    internal static void RegisterEditorsForRegisteredTypes() {
        airshipTypeToEditorTypes.Clear();
        editors.Clear();
        
        var typeEditorAttributes = TypeCache.GetTypesWithAttribute<AirshipEditorAttribute>();
        foreach (var editor in typeEditorAttributes) {
            var attr = editor.GetCustomAttributes<AirshipEditorAttribute>();
            foreach (var editorAttribute in attr) {
                var pathType = AirshipBuildInfo.Instance.GetTypeByName(editorAttribute.TypeName);
                if (pathType == null) {
                    Debug.LogWarning($"Cannot find type {editorAttribute.TypeName} from types");
                    continue;
                }
                
                Debug.Log($"Register type {pathType.UniqueId}");
                if (!AirshipCustomEditors.airshipTypeToEditorTypes.TryGetValue(pathType, out var _)) {
                    AirshipCustomEditors.airshipTypeToEditorTypes.Add(pathType, editor);
                }
            }
        }
        
        Debug.Log($"Registered {airshipTypeToEditorTypes.Count} custom editors");
    }
    
    [InitializeOnLoadMethod]
    internal static void RegisterCustomEditors() {
        var pathEditorAttributes = TypeCache.GetTypesWithAttribute<AirshipComponentEditorAttribute>();
        foreach (var editor in pathEditorAttributes) {
            var attr = editor.GetCustomAttributes<AirshipComponentEditorAttribute>();
            foreach (var editorAttribute in attr) {
                if (!AirshipCustomEditors.editorTypes.TryGetValue(editorAttribute.FilePath, out var _)) {
                    AirshipCustomEditors.editorTypes.Add(editorAttribute.FilePath, editor);
                }
            }
        }
        
        RegisterEditorsForRegisteredTypes();
    }
    
    public static Type GetEditorForTypeName(string typeName) {
        var pathType = AirshipBuildInfo.Instance.GetTypeByName(typeName);
        if (pathType == null) return null;
        
        if (airshipTypeToEditorTypes.TryGetValue(pathType, out var editorType)) {
            return editorType;
        }

        return EditorIntegrationsConfig.instance.experimentalCustomEditor ? typeof(DefaultAirshipComponentEditor) : null;
    }
    
    public static Type GetEditorForFilePath(string filePath) {
        if (editorTypes.TryGetValue(filePath, out var editorType)) {
            return editorType;
        }

        return EditorIntegrationsConfig.instance.experimentalCustomEditor ? typeof(DefaultAirshipComponentEditor) : null;
    }

    public static bool TryGetEditor(AirshipComponent component, Type type, out AirshipEditor editor) {
        return editors.TryGetValue(component.GetInstanceID(), out editor);
    }
    
    public static AirshipEditor GetEditor(AirshipComponent component, Type type, SerializedObject serializedObject) {
        if (editors.TryGetValue(component.GetInstanceID(), out var editor)) {
            editor.serializedObject ??= new AirshipSerializedObject();
            editor.serializedObject.Update(editor, serializedObject, component.script.m_metadata);
            return editor;
        }

        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        editor.serializedObject ??= new AirshipSerializedObject();
        editor.serializedObject.Update(editor, serializedObject, component.script.m_metadata);
        editors.Add(component.GetInstanceID(), editor);
        return editor;
    }

    internal static void DestroyEditor(int editorId) {
        if (editors.TryGetValue(editorId, out var editor)) {
            Debug.Log($"Destroying editor {editorId}");
            editors.Remove(editorId);
            Object.DestroyImmediate(editor);
        }
    }
}