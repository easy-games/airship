using System;
using System.Collections.Generic;
using System.Reflection;
using Luau;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The custom editor namespace for the Airship Editor
/// </summary>
public static class AirshipCustomEditors {
    private static Dictionary<string, Type> editorTypes = new();
    private static Dictionary<AirshipType, Type> airshipTypeToEditorTypes = new();
    
    private static Dictionary<int, AirshipEditor> editors = new();

    internal const EditorInspectorMode DefaultInspectorMode = EditorInspectorMode.UseLegacyInspector;
    internal static EditorInspectorMode EditorInspectorMode {
        get {
            var instance = EditorIntegrationsConfig.instance;
            if (instance.editorInspectorMode == EditorInspectorMode.Default) return DefaultInspectorMode;
            return instance.editorInspectorMode;
        }
        set {
            var instance = EditorIntegrationsConfig.instance;
            if (value == instance.editorInspectorMode) return;
            
            if (value == EditorInspectorMode.UseLegacyInspector) {
                editors.Clear();
                airshipTypeToEditorTypes.Clear();
            } else if (value == EditorInspectorMode.UseNewInspector || (value == EditorInspectorMode.Default &&
                                                                        DefaultInspectorMode ==
                                                                        EditorInspectorMode.UseNewInspector)) {
                RegisterEditorsForRegisteredTypes();
            }

            instance.editorInspectorMode = value;
        }
    }
    
    internal static void RegisterEditorsForRegisteredTypes() {
        airshipTypeToEditorTypes.Clear();
        editors.Clear();
        
        var typeEditorAttributes = TypeCache.GetTypesWithAttribute<AirshipEditorAttribute>();
        foreach (var editor in typeEditorAttributes) {
            var attr = editor.GetCustomAttributes<AirshipEditorAttribute>();
            foreach (var editorAttribute in attr) {
                var pathType = string.IsNullOrEmpty( editorAttribute.FilePath) ? 
                    AirshipBuildInfo.Instance.GetTypeByName(editorAttribute.TypeName) :  
                    AirshipBuildInfo.Instance.GetTypeByPathAndName(editorAttribute.FilePath, editorAttribute.TypeName);
                if (pathType == null) {
                    Debug.LogWarning($"Failed to load custom inspector for type {editorAttribute.TypeName}, type is not found in project.");
                    continue;
                }
                
                if (!AirshipCustomEditors.airshipTypeToEditorTypes.TryGetValue(pathType, out var _)) {
                    AirshipCustomEditors.airshipTypeToEditorTypes.Add(pathType, editor);
                }
            }
        }
        
#if AIRSHIP_INTERNAL
        Debug.Log($"Registered {airshipTypeToEditorTypes.Count} custom editors");
#endif
    }
    
    [InitializeOnLoadMethod]
    internal static void RegisterCustomEditors() {
        RegisterEditorsForRegisteredTypes();

        EditorApplication.playModeStateChanged += change => {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode) {
                RegisterEditorsForRegisteredTypes();
            }
        };
    }
    
    internal static Type GetEditorForTypeName(string typeName) {
        if (EditorInspectorMode != EditorInspectorMode.UseNewInspector) return null;
        
        var pathType = AirshipBuildInfo.Instance.GetTypeByName(typeName);
        if (pathType == null) return null;
        
        if (airshipTypeToEditorTypes.TryGetValue(pathType, out var editorType)) {
            return editorType;
        }

        return typeof(DefaultAirshipComponentEditor);
    }
    
    internal static Type GetEditorForFilePath(string filePath) {
        if (EditorInspectorMode != EditorInspectorMode.UseNewInspector) return null;
        
        if (editorTypes.TryGetValue(filePath, out var editorType)) {
            return editorType;
        }

        return typeof(DefaultAirshipComponentEditor);
    }

    internal static bool TryGetEditor(AirshipComponent component, Type type, out AirshipEditor editor) {
        return editors.TryGetValue(component.GetInstanceID(), out editor);
    }
    
    internal static AirshipEditor GetEditor(AirshipComponent component, Type type, SerializedObject serializedObject) {
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


    internal static AirshipEditor GetComponentEditorForType(AirshipType airshipType, AirshipComponent component, AirshipSerializedObject serializedObject) {
        if (!airshipType.AirshipBehaviour) return null;
        var editorType = AirshipCustomEditors.GetEditorForTypeName(airshipType.Name);
        var editor = AirshipCustomEditors.GetEditor(component, editorType, serializedObject);
        return editor;
    }

    /// <summary>
    /// Will attempt to grab the editor for the given serialized value - if applicable
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static AirshipEditor GetEditor(AirshipSerializedValue value) {
        if (!value.isAirshipType) return null;

        var component = value.objectReferenceValue;
        if (component == null) return null;
        
        if (component is AirshipComponent airshipComponent) {
            return GetEditor(airshipComponent);
        };
        
        // TODO: In future we'll support Serializable objects & ScriptableObjects through here too.
        return null;
    }

    /// <summary>
    /// Grabs the editor for the specific AirshipComponent
    /// </summary>
    /// <param name="component">The component to get the editor for</param>
    /// <returns></returns>
    public static AirshipEditor GetEditor(AirshipComponent component) {
        if (component != null && component.script != null) {
            var airshipType = component.GetAirshipType();
            
            var serializedObject = new AirshipSerializedObject();
            serializedObject.Update(null, new SerializedObject(component), component.metadata);
            
            return GetComponentEditorForType(airshipType, component, serializedObject);
        }

        return null;
    }
    
    internal static void DestroyEditor(int editorId) {
        if (editors.TryGetValue(editorId, out var editor)) {
            Debug.Log($"Destroying editor {editorId}");
            editors.Remove(editorId);
            Object.DestroyImmediate(editor);
        }
    }
}