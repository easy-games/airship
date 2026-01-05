using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Codice.CM.Common.Tree.Partial;
using HandlebarsDotNet.PathStructure;
using JetBrains.Annotations;
using TypescriptAst;
using Luau;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
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

    internal class CustomPropertyDrawerInfo {
        public Type EditorType { get; }
        public AirshipType AirshipType { get; }
        public CustomAirshipPropertyDrawerAttribute PropertyDrawerAttribute { get; }
        public AirshipPropertyDrawer PropertyDrawer { get; internal set; }
        public CustomPropertyDrawerInfo(Type editorType, AirshipType airshipType, CustomAirshipPropertyDrawerAttribute editorAttribute) {
            EditorType = editorType;
            PropertyDrawerAttribute = editorAttribute;
            AirshipType = airshipType;
        }
    }
    
    #region Fields
    private static Dictionary<AirshipType, CustomEditorInfo> airshipTypeToCustomEditor = new();
    private static Dictionary<AirshipType, CustomPropertyDrawerInfo> airshipTypeToCustomPropertyDrawer = new();
    private static Dictionary<int, AirshipEditor> instanceToAirshipEditor = new();
    
    private static Dictionary<Type, AirshipPropertyDecorator> typeToEditorPropertyDecorator = new();
    
    private static Dictionary<string, Type> decoratorNameToEditorType = new();
    private static Dictionary<string, AirshipGUIDrawer> decoratorNameToGUIDrawer = new();
    
    internal const string inspectorModeKey = "AirshipBetaInspectorMode";
    internal const EditorInspectorMode DefaultInspectorMode = EditorInspectorMode.UseNewInspector;
    #endregion
    
    #region Properties
    internal static AirshipEditor CurrentEditor { get; set; }
    
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
    private static bool RegisterPropertyDrawer(Type editorType, CustomAirshipPropertyDrawerAttribute propertyDrawerAttribute) {
        var typeName = propertyDrawerAttribute.TypeName;
        var filePath = propertyDrawerAttribute.AssetPath;
        if (!AirshipBuildInfo.TryGetInstance(out var buildInfo)) return false;
        
        var pathType = string.IsNullOrEmpty(filePath) ? 
            buildInfo.GetTypeByName(typeName) :  
            buildInfo.GetTypeByPathAndName(filePath, typeName);
            
        if (pathType == null) {
            return false;
        }
        
        if (!AirshipCustomEditors.airshipTypeToCustomPropertyDrawer.TryGetValue(pathType, out var _)) {
            Debug.Log($"Create custom property drawer for {pathType.UniqueId} - {editorType.Name}");
            AirshipCustomEditors.airshipTypeToCustomPropertyDrawer.Add(pathType, new CustomPropertyDrawerInfo(editorType, pathType, propertyDrawerAttribute));
        }

        return true;
    }

    private static void RegisterPropertyDrawer(Type editorType,
        CustomAirshipDecoratorDrawerAttribute propertyDrawerAttribute) {
        if (decoratorNameToGUIDrawer.TryGetValue(propertyDrawerAttribute.DecoratorName, out _)) return;
        var instance = (AirshipGUIDrawer) Activator.CreateInstance(editorType);
        decoratorNameToGUIDrawer.Add(propertyDrawerAttribute.DecoratorName, instance);
    }
    
    private static bool RegisterEditor(Type editorType, CustomAirshipEditorAttribute editorAttribute) {
        var typeName = editorAttribute.TypeName;
        var filePath = editorAttribute.AssetPath;
        if (!AirshipBuildInfo.TryGetInstance(out var buildInfo)) return false;
        
        var pathType = string.IsNullOrEmpty(filePath) ? 
            buildInfo.GetTypeByName(typeName) :  
            buildInfo.GetTypeByPathAndName(filePath, typeName);
            
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
        airshipTypeToCustomPropertyDrawer.Clear();
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
            if (!RegisterEditor(editorType, attr)) {
                Debug.LogWarning($"Could not register editor {editorType.Name}");
            }
        }

        var propertyEditorAttributes = TypeCache.GetTypesWithAttribute<CustomAirshipPropertyDrawerAttribute>();
        foreach (var editorType in propertyEditorAttributes) {
            var propertyDrawerAttribute = editorType.GetCustomAttribute<CustomAirshipPropertyDrawerAttribute>();
            RegisterPropertyDrawer(editorType, propertyDrawerAttribute);
        }

        var decoratorPropertyAttributes = TypeCache.GetTypesWithAttribute<CustomAirshipDecoratorDrawerAttribute>();
        foreach (var editorType in decoratorPropertyAttributes) {
            var propertyDrawerAttribute = editorType.GetCustomAttribute<CustomAirshipDecoratorDrawerAttribute>();
            RegisterPropertyDrawer(editorType, propertyDrawerAttribute);
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
        Debug.Log($"Registered {airshipTypeToCustomEditor.Count} custom editors, {airshipTypeToCustomPropertyDrawer.Count} property drawers and {decoratorNameToEditorType.Count} decorators.");
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
    
    internal static Type GetEditorTypeForTypeName(string typeName, AirshipDeclarationType? airshipDeclarationType = null) {
        if (!UseNewInspector) return null;
      
        
        var pathType = AirshipBuildInfo.Instance.GetTypeByName(typeName);
        if (pathType == null) return null;

        airshipDeclarationType ??= pathType.DeclarationType;
        
        if (airshipTypeToCustomEditor.TryGetValue(pathType, out var editorType)) {
            return editorType.EditorType;
        }

        foreach (var baseType in pathType.BaseTypes) {
            if (!airshipTypeToCustomEditor.TryGetValue(baseType, out editorType)) continue;
            if (editorType.EditorAttribute.EditorForChildClasses) return editorType.EditorType;
        }
        
        return airshipDeclarationType switch {
            AirshipDeclarationType.Unknown => null,
            AirshipDeclarationType.AirshipBehaviour => typeof(DefaultAirshipComponentEditor),
            AirshipDeclarationType.Enum => null,
            AirshipDeclarationType.AirshipScriptableObject => typeof(DefaultAirshipScriptableObjectEditor),
            AirshipDeclarationType.SerializableClass => typeof(DefaultAirshipSerializableObjectEditor),
            _ => null
        };
    }

    internal static bool TryGetEditorForComponent(AirshipComponent component, Type type, out AirshipEditor editor) {
        return instanceToAirshipEditor.TryGetValue(component.GetInstanceID(), out editor);
    }

    internal static bool TryGetEditorForScriptableObject(AirshipScriptableObject scriptableObject, Type type,
        out AirshipEditor editor) {
        return instanceToAirshipEditor.TryGetValue(scriptableObject.GetInstanceID(), out editor);
    }
    
    private static AirshipEditor GetOrCreateEditorForObject(Object target, Type type) {
        var instanceId = target.GetInstanceID();
        
        if (instanceToAirshipEditor.TryGetValue(instanceId, out var editor)) {
            if (editor != null) {
                return editor;
            }

            instanceToAirshipEditor.Remove(instanceId); // if our editor is null (e.g. after play mode)
        }
        
        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        instanceToAirshipEditor.Add(instanceId, editor);
        return editor;
    }
    
    internal static AirshipEditor GetEditorForComponent(AirshipComponent component, Type type, SerializedObject serializedObject) {
        var editor = GetOrCreateEditorForObject(component, type);
        editor.serializedObject ??= new AirshipSerializedObject();
        editor.serializedObject.Update(editor, serializedObject, component.script.m_metadata);
        return editor;
    }
    
#if AIRSHIPEX_CLASS_OBJECT
    internal static AirshipEditor GetEditorForClass(AirshipSerializableClassObject component, Type type, SerializedObject serializedObject) {
        if (instanceToAirshipEditor.TryGetValue(component.GetInstanceID(), out var editor)) {
            editor.serializedObject ??= new AirshipSerializedObject();
            editor.serializedObject.Update(editor, serializedObject, component.metadata);
            return editor;
        }

        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        editor.serializedObject ??= new AirshipSerializedObject();
        editor.serializedObject.Update(editor, serializedObject, component.metadata);
        instanceToAirshipEditor.Add(component.GetInstanceID(), editor);
        return editor;
    }
#endif
    
    internal static AirshipEditor GetEditorForScriptableObject(AirshipScriptableObject scriptableObject, Type type, SerializedObject serializedObject) {
        var editor = GetOrCreateEditorForObject(scriptableObject, type);
        editor.serializedObject ??= new AirshipSerializedObject();
        editor.serializedObject.Update(editor, serializedObject, scriptableObject.script.m_metadata);
        return editor;
    }

    internal static AirshipEditor GetScriptableObjectEditorForType(AirshipType airshipType, AirshipScriptableObject scriptableObject, AirshipSerializedObject serializedObject) {
        if (airshipType.DeclarationType != AirshipDeclarationType.AirshipScriptableObject) return null;
        var editorType = AirshipCustomEditors.GetEditorTypeForTypeName(airshipType.Name);
        var editor = AirshipCustomEditors.GetEditorForScriptableObject(scriptableObject, editorType, serializedObject);
        return editor;
    }

    internal static AirshipEditor GetComponentEditorForType(AirshipType airshipType, AirshipComponent component,
        AirshipSerializedObject serializedObject) {
        if (airshipType.DeclarationType != AirshipDeclarationType.AirshipBehaviour) return null;
        var editorType = AirshipCustomEditors.GetEditorTypeForTypeName(airshipType.Name);
        var editor = AirshipCustomEditors.GetEditorForComponent(component, editorType, serializedObject);
        return editor;
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

    [CanBeNull]
    internal static AirshipGUIDrawer GetDecoratorDrawer(string name) {
        return decoratorNameToGUIDrawer.GetValueOrDefault(name);
    }
    
    /// <summary>
    /// Gets the property drawer for the property (if applicable)
    /// </summary>
    /// <param name="property">The property to get the property drawer for</param>
    /// <returns>A property drawer, or null if it is not an airship type, or no property drawer is set</returns>
    [CanBeNull]
    public static AirshipPropertyDrawer GetPropertyDrawer(AirshipSerializedValue property) {
        if (!property.isAirshipType) return null;
        var airshipType = property.airshipType;

        if (airshipTypeToCustomPropertyDrawer.TryGetValue(airshipType, out var customPropertyDrawerInfo)) {
            return customPropertyDrawerInfo.PropertyDrawer ?? (customPropertyDrawerInfo.PropertyDrawer =
                (AirshipPropertyDrawer)Activator.CreateInstance(customPropertyDrawerInfo.EditorType));
        }

        return null;
    }
    
    /// <summary>
    /// Get an AirshipEditor for the given serialized property - will only work with AirshipBehaviour and AirshipScriptableObject properties
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static AirshipEditor GetEditor(AirshipSerializedProperty value) {
        if (!value.isAirshipType) return null;

        var objectReferenceValue = value.objectReferenceValue;
        if (objectReferenceValue == null) return null;

        return objectReferenceValue switch {
            AirshipComponent airshipComponent => GetEditor(airshipComponent),
            AirshipScriptableObject scriptableObject => GetEditor(scriptableObject),
            _ => null
        };
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
    
    /// <summary>
    /// Get the editor for the given AirshipScriptableObject
    /// </summary>
    /// <param name="scriptableObject">The scriptable object to get the editor for</param>
    /// <returns>The editor, if it exists otherwise null</returns>
    public static AirshipEditor GetEditor(AirshipScriptableObject scriptableObject) {
        if (scriptableObject != null && scriptableObject.script != null) {
            var airshipType = scriptableObject.GetAirshipType();
            scriptableObject.ReconcileMetadata(ReconcileSource.Inspector);
            
            var serializedObject = new AirshipSerializedObject();
            serializedObject.Update(null, new SerializedObject(scriptableObject), serializedObject.metadata);
            return GetScriptableObjectEditorForType(airshipType, scriptableObject, serializedObject);
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