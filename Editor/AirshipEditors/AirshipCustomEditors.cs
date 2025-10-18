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

    internal static ComponentEditorVersion editorVersion {
        get {
            var instance = EditorIntegrationsConfig.instance;
            if (instance.componentEditorVersion == ComponentEditorVersion.Default) return ComponentEditorVersion.UseLegacyInspector;
            return instance.componentEditorVersion;
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

        EditorApplication.playModeStateChanged += change => {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode) {
                RegisterEditorsForRegisteredTypes();
            }
        };
    }
    
    internal static Type GetEditorForTypeName(string typeName) {
        if (editorVersion != ComponentEditorVersion.UseNewInspector) return null;
        
        var pathType = AirshipBuildInfo.Instance.GetTypeByName(typeName);
        if (pathType == null) return null;
        
        if (airshipTypeToEditorTypes.TryGetValue(pathType, out var editorType)) {
            return editorType;
        }

        return typeof(DefaultAirshipComponentEditor);
    }
    
    internal static Type GetEditorForFilePath(string filePath) {
        if (editorVersion != ComponentEditorVersion.UseNewInspector) return null;
        
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