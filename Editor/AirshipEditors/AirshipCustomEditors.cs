using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TypescriptAst;
using Luau;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The custom editor namespace for the Airship Editor
/// </summary>
public static class AirshipCustomEditors {
    /// <summary>
    /// Contains information about the custom Airship editor
    /// </summary>
    internal class CustomEditorInfo {
        public Type EditorType { get; }
        public AirshipType AirshipType { get; }
        public CustomAirshipEditorAttribute EditorAttribute { get; }
        public CustomEditorInfo(Type editorType, AirshipType airshipType, CustomAirshipEditorAttribute editorAttribute) {
            EditorType = editorType;
            EditorAttribute = editorAttribute;
            AirshipType = airshipType;
        }
    }
    
    #region Fields
    private static Dictionary<AirshipType, CustomEditorInfo> airshipTypeToCustomEditor = new();
    private static Dictionary<int, AirshipEditor> instanceToAirshipEditor = new();
    
    private static Dictionary<Type, AirshipPropertyDecorator> typeToEditorPropertyDecorator = new();
    private static Dictionary<string, Type> decoratorNameToEditorType = new();
    
    internal const string inspectorModeKey = "AirshipBetaInspectorMode";
    internal const EditorInspectorMode DefaultInspectorMode = EditorInspectorMode.UseLegacyInspector;
    #endregion
    
    #region Properties
    /// <summary>
    /// A list of the active custom editors
    /// </summary>
    internal static IEnumerable<CustomEditorInfo> CustomEditors {
        get => airshipTypeToCustomEditor.Values;
    }

    /// <summary>
    /// A list of all the active airship editors (includes the default inspectors)
    /// </summary>
    internal static IEnumerable<AirshipEditor> AllEditors {
        get => instanceToAirshipEditor.Values;
    }
    
    internal static EditorInspectorMode UserInspectorMode {
        get {
            var value = (EditorInspectorMode) EditorPrefs.GetInt(inspectorModeKey, (int) EditorInspectorMode.Default);
            return value;
        }
        set {
            var current = (EditorInspectorMode) EditorPrefs.GetInt(inspectorModeKey, (int) EditorInspectorMode.Default);
            if (value == current) return;
            
            if (value == EditorInspectorMode.UseLegacyInspector) {
                instanceToAirshipEditor.Clear();
                airshipTypeToCustomEditor.Clear();
            } else if (value == EditorInspectorMode.UseNewInspector || (value == EditorInspectorMode.Default &&
                                                                        DefaultInspectorMode ==
                                                                        EditorInspectorMode.UseNewInspector)) {
                RegisterEditorsForRegisteredTypes();
            }
            
            EditorPrefs.SetInt(inspectorModeKey, (int) value);
        }
    }
    
    internal static bool UseNewInspector {
        get {
            var inspector = UserInspectorMode;
            if (inspector == EditorInspectorMode.Default) inspector = DefaultInspectorMode;
            return inspector == EditorInspectorMode.UseNewInspector;
        }
    }
    
    #endregion
    
    #region Editor Registration Methods
    
    private static bool RegisterEditor(Type editorType, CustomAirshipEditorAttribute editorAttribute) {
        var typeName = editorAttribute.TypeName;
        var filePath = editorAttribute.FilePath;
                
        var pathType = string.IsNullOrEmpty(filePath) ? 
            AirshipBuildInfo.Instance.GetTypeByName(typeName) :  
            AirshipBuildInfo.Instance.GetTypeByPathAndName(filePath, typeName);
                
        if (pathType == null) {
            return false;
        }
                
        if (!AirshipCustomEditors.airshipTypeToCustomEditor.TryGetValue(pathType, out var _)) {
            AirshipCustomEditors.airshipTypeToCustomEditor.Add(pathType, new CustomEditorInfo(editorType, pathType, editorAttribute));
        }

        return true;
    }
    
    internal static void RegisterEditorsForRegisteredTypes() {
        airshipTypeToCustomEditor.Clear();
        decoratorNameToEditorType.Clear();
        typeToEditorPropertyDecorator.Clear();
        instanceToAirshipEditor.Clear();

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
            decoratorNameToEditorType.Add(attr.Name, decoratorAttributeType);
            
            var propertyDecorator = (AirshipPropertyDecorator)ScriptableObject.CreateInstance(decoratorAttributeType);
            propertyDecorator.attribute = attr;
            typeToEditorPropertyDecorator.Add(decoratorAttributeType, propertyDecorator);

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
        Debug.Log($"Registered {airshipTypeToCustomEditor.Count} custom editors, {decoratorNameToEditorType.Count} decorators.");
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
    
    #endregion
    
    #region Custom Editor Query Methods
    
    /// <summary>
    /// Gets all editors of the given custom editor info
    /// </summary>
    /// <param name="editorInfo">The custom editor to grab the editors from</param>
    /// <returns>A list of Airship editors that match the given custom editor</returns>
    internal static IEnumerable<AirshipEditor> GetEditors(CustomEditorInfo editorInfo) {
        var instances = new List<AirshipEditor>();
        foreach (var instance in instanceToAirshipEditor) {
            if (instance.Value.GetType() == editorInfo.EditorType) instances.Add(instance.Value);
        }

        return instances;
    }
    
    internal static Type GetEditorTypeForTypeName(string typeName) {
        if (!UseNewInspector) return null;
        
        var pathType = AirshipBuildInfo.Instance.GetTypeByName(typeName);
        if (pathType == null) return null;
        
        if (airshipTypeToCustomEditor.TryGetValue(pathType, out var editorType)) {
            return editorType.EditorType;
        }

        return typeof(DefaultAirshipComponentEditor);
    }

    internal static bool TryGetEditorForComponent(AirshipComponent component, Type type, out AirshipEditor editor) {
        return instanceToAirshipEditor.TryGetValue(component.GetInstanceID(), out editor);
    }
    
    internal static AirshipEditor GetEditorForComponent(AirshipComponent component, Type type, SerializedObject serializedObject) {
        if (instanceToAirshipEditor.TryGetValue(component.GetInstanceID(), out var editor)) {
            editor.serializedObject ??= new AirshipSerializedObject();
            editor.serializedObject.Update(editor, serializedObject, component.script.m_metadata);
            return editor;
        }

        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        editor.serializedObject ??= new AirshipSerializedObject();
        editor.serializedObject.Update(editor, serializedObject, component.script.m_metadata);
        instanceToAirshipEditor.Add(component.GetInstanceID(), editor);
        return editor;
    }


    internal static AirshipEditor GetComponentEditorForType(AirshipType airshipType, AirshipComponent component, AirshipSerializedObject serializedObject) {
        if (!airshipType.AirshipBehaviour) return null;
        var editorType = AirshipCustomEditors.GetEditorTypeForTypeName(airshipType.Name);
        var editor = AirshipCustomEditors.GetEditorForComponent(component, editorType, serializedObject);
        return editor;
    }

    /// <summary>
    /// Get an AirshipEditor for the given serialized property - can be used to embed inline
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static AirshipEditor GetEditor(AirshipSerializedProperty value) {
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
    /// Gets the editor's property decorator for the given luau property decorator
    /// </summary>
    /// <param name="decorator"></param>
    /// <param name="propertyDecorator"></param>
    /// <returns></returns>
    internal static bool TryGetDecorator(LuauMetadataDecoratorElement decorator, out AirshipPropertyDecorator propertyDecorator) {
        if (decoratorNameToEditorType.TryGetValue(decorator.name, out var decoratorType)) {
            if (!typeToEditorPropertyDecorator.TryGetValue(decoratorType, out propertyDecorator)) {
                propertyDecorator = (AirshipPropertyDecorator)ScriptableObject.CreateInstance(decoratorType);
                typeToEditorPropertyDecorator.Add(decoratorType, propertyDecorator);
            }
            
            return true;
        }

        propertyDecorator = default;
        return false;
    }

    /// <summary>
    /// Get the editor for the given AirshipComponent
    /// </summary>
    /// <param name="component">The component to get the editor for</param>
    /// <returns>The editor, if it exists otherwise null</returns>
    public static AirshipEditor GetEditor(AirshipComponent component) {
        if (component != null && component.script != null) {
            var airshipType = component.GetAirshipType();
            
            var serializedObject = new AirshipSerializedObject();
            serializedObject.Update(null, new SerializedObject(component), component.metadata);
            
            return GetComponentEditorForType(airshipType, component, serializedObject);
        }

        return null;
    }
    
    #endregion
    
    private static void DestroyEditor(int editorId) {
        if (instanceToAirshipEditor.TryGetValue(editorId, out var editor)) {
            instanceToAirshipEditor.Remove(editorId);
            Object.DestroyImmediate(editor);
        }
    }

    /// <summary>
    /// Destroy the editor for the given airship component
    /// </summary>
    /// <param name="component">The airship component</param>
    internal static void DestroyEditor(AirshipComponent component) {
        if (component.script != null && component.metadata != null && !component) {
            DestroyEditor(component.GetInstanceID());
        }
    }
}