using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Editor.Typescript;
using Luau;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The custom editor namespace for the Airship Editor
/// </summary>
public static class AirshipCustomEditors {
    private static Dictionary<string, Type> editorTypes = new();
    private static Dictionary<string, Type> decoratorTypes = new();
    private static Dictionary<AirshipType, CustomEditor> airshipTypeToEditor = new();
    
    private static Dictionary<int, AirshipEditor> editors = new();

    internal class CustomEditor {
        public Type Type { get; }
        public CustomAirshipEditorAttribute Attribute { get; }
        public CustomEditor(Type type, CustomAirshipEditorAttribute attribute) {
            Type = type;
            Attribute = attribute;
        }
    }
    
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
                airshipTypeToEditor.Clear();
            } else if (value == EditorInspectorMode.UseNewInspector || (value == EditorInspectorMode.Default &&
                                                                        DefaultInspectorMode ==
                                                                        EditorInspectorMode.UseNewInspector)) {
                RegisterEditorsForRegisteredTypes();
            }

            instance.editorInspectorMode = value;
        }
    }

    private static void RegisterEditor(Type editorType, CustomAirshipEditorAttribute editorAttribute) {
        var typeName = editorAttribute.TypeName;
        var filePath = editorAttribute.FilePath;
                
        var pathType = string.IsNullOrEmpty(filePath) ? 
            AirshipBuildInfo.Instance.GetTypeByName(typeName) :  
            AirshipBuildInfo.Instance.GetTypeByPathAndName(filePath, typeName);
                
        if (pathType == null) {
            Debug.LogWarning($"Failed to load custom inspector for type {typeName}, type is not found in project.");
            return;
        }
                
        if (!AirshipCustomEditors.airshipTypeToEditor.TryGetValue(pathType, out var _)) {
            AirshipCustomEditors.airshipTypeToEditor.Add(pathType, new CustomEditor(editorType, editorAttribute));
        }
    }
    
    internal static void RegisterEditorsForRegisteredTypes() {
        airshipTypeToEditor.Clear();
        decoratorTypes.Clear();
        decoratorInstances.Clear();
        editors.Clear();
        
        var typeEditorAttributes = TypeCache.GetTypesWithAttribute<CustomAirshipEditorAttribute>();
        
        foreach (var editorType in typeEditorAttributes) {
            var internalAttr = editorType.GetCustomAttribute<CustomAirshipCoreEditorAttribute>();
            if (internalAttr != null) {
                RegisterEditor(editorType, internalAttr);
                continue;
            }
            
            var attr = editorType.GetCustomAttribute<CustomAirshipEditorAttribute>();
            RegisterEditor(editorType, attr);
        }

        var decoratorStatements = new List<IStatement>();

        var decoratorAttributes = TypeCache.GetTypesWithAttribute<CustomAirshipDecoratorAttribute>();
        foreach (var decoratorAttributeType in decoratorAttributes) {
            var attr = decoratorAttributeType.GetCustomAttribute<CustomAirshipDecoratorAttribute>();
            decoratorTypes.Add(attr.Name, decoratorAttributeType);
            
            var propertyDecorator = (AirshipPropertyDecorator)ScriptableObject.CreateInstance(decoratorAttributeType);
            propertyDecorator.attribute = attr;
            decoratorInstances.Add(decoratorAttributeType, propertyDecorator);

            var decoratorParams = propertyDecorator.GetFunctionDeclaration();
            if (decoratorParams != null) {
                decoratorStatements.Add(new TsComment() { IsJsDoc = true, Text = $"Generated editor decorator '{attr.Name}' from {decoratorAttributeType.FullName}" });
                decoratorStatements.Add(decoratorParams);
            }
        }
        
        if (decoratorStatements.Count > 0) {
            var sourceFile = new TsSourceFile() {
                Statements = decoratorStatements.ToArray(),
            };
            
            File.WriteAllText("Assets/decorators.d.ts", sourceFile.ToString());
        } else if (File.Exists("Assets/decorators.d.ts")) {
            File.Delete("Assets/decorators.d.ts");
        }
        
#if AIRSHIP_INTERNAL
        Debug.Log($"Registered {airshipTypeToEditor.Count} custom editors, {decoratorTypes.Count} decorators.");
#endif
    }

    [InitializeOnLoadMethod]
    internal static void InitializeEditorSymbols() {
        string currentDefines = PlayerSettings.GetScriptingDefineSymbols(
            UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
        
        HashSet<string> defines = new HashSet<string>(currentDefines.Split(';')) {
            "AIRSHIP_EDITOR_API"
        };
        string newDefines = string.Join(";", defines);
        
        if (newDefines != currentDefines) {
            PlayerSettings.SetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), newDefines);
        }
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
        
        if (airshipTypeToEditor.TryGetValue(pathType, out var editorType)) {
            return editorType.Type;
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

    private static Dictionary<Type, AirshipPropertyDecorator> decoratorInstances = new();
    internal static bool GetDecorator(LuauMetadataDecoratorElement decorator, out AirshipPropertyDecorator propertyDecorator) {
        if (decoratorTypes.TryGetValue(decorator.name, out var decoratorType)) {
            if (!decoratorInstances.TryGetValue(decoratorType, out propertyDecorator)) {
                propertyDecorator = (AirshipPropertyDecorator)ScriptableObject.CreateInstance(decoratorType);
                decoratorInstances.Add(decoratorType, propertyDecorator);
            }
            
            return true;
        }

        propertyDecorator = default;
        return false;
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
            Debug.LogFormat(LogType.Warning, LogOption.None, null, "Destroyed editor");
            Debug.Log($"Destroying editor {editorId}");
            editors.Remove(editorId);
            Object.DestroyImmediate(editor);
        }
    }
}