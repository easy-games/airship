#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Globalization;
using System;
using System.Collections.Generic;
using Luau;
using Object = System.Object;

[CustomEditor(typeof(ScriptBinding))]
public class ScriptBindingEditor : Editor {
    /** Maps (script name, prop name) to whether a foldout is open */
    private static Dictionary<(string, string), bool> _openPropertyFoldouts = new();
    
    public override void OnInspectorGUI() {
        serializedObject.Update();

        ScriptBinding binding = (ScriptBinding)target;

        if (binding.m_script == null && !string.IsNullOrEmpty(binding.m_fileFullPath)) {
            // Attempt to find the script based on the filepath:
            // Debug.Log("Attempting to reconcile script asset from path...");
            
            // Check if path is the old style, and readjust if so:
            var path = binding.m_fileFullPath;
            if (!path.StartsWith("Assets/Bundles/")) {
                path = "Assets/Bundles/" + path;
                if (!path.EndsWith(".lua")) {
                    path += ".lua";
                }

                binding.m_fileFullPath = path;
                serializedObject.FindProperty("m_fileFullPath").stringValue = path;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
            
            binding.SetScriptFromPath(binding.m_fileFullPath);
            if (binding.m_script != null) {
                // Debug.Log("Script asset found");
            } else {
                Debug.LogWarning($"Failed to load script asset: {binding.m_fileFullPath}");
            }
        }

        if (binding.m_script != null) {
            var componentName = binding.m_script.m_metadata.name;
            if (!string.IsNullOrEmpty(componentName)) {
                var original = EditorStyles.label.fontStyle;
                EditorStyles.label.fontStyle = FontStyle.Bold;
                GUILayout.Label(componentName, EditorStyles.label);
                EditorStyles.label.fontStyle = original;
            }
            
            if (ShouldReconcile(binding)) {
                binding.ReconcileMetadata();
                serializedObject.Update();
            }

            CheckDefaults(binding);
        }
        
        DrawScriptBindingProperties(binding);

        if (binding.m_script != null) {
            var metadata = serializedObject.FindProperty("m_metadata");
            var metadataName = metadata.FindPropertyRelative("name");
            if (!string.IsNullOrEmpty(metadataName.stringValue)) {
                DrawBinaryFileMetadata(binding, metadata);
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    private void CheckDefaults(ScriptBinding binding) {
        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.m_script.m_metadata.properties;

        var setDefault = false;

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var propName = metadataProperty.FindPropertyRelative("name").stringValue;
            var originalProperty = originalMetadataProperties.Find((p) => p.name == propName);
            
            var modified = metadataProperty.FindPropertyRelative("modified");
            if (modified.boolValue) continue;
            
            var serializedValueProperty = metadataProperty.FindPropertyRelative("serializedValue");
            if (serializedValueProperty.stringValue != originalProperty.serializedValue) {
                serializedValueProperty.stringValue = originalProperty.serializedValue;
                setDefault = true;
            }
        }

        if (setDefault) {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }

    private bool ShouldReconcile(ScriptBinding binding) {
        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.m_script.m_metadata.properties;

        if (metadataProperties.arraySize != originalMetadataProperties.Count) {
            return true;
        }

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var originalProperty = originalMetadataProperties[i];

            if (originalProperty.name != metadataProperty.FindPropertyRelative("name").stringValue) {
                return true;
            }

            if (originalProperty.type != metadataProperty.FindPropertyRelative("type").stringValue) {
                return true;
            }

            var decorators = metadataProperty.FindPropertyRelative("decorators");
            if (originalProperty.decorators.Count != decorators.arraySize) {
                return true;
            }

            for (var j = 0; j < decorators.arraySize; j++)
            {
                var decorator = decorators.GetArrayElementAtIndex(j);
                var originalDecorator = originalProperty.decorators[j];
                if (originalDecorator.name != decorator.FindPropertyRelative("name").stringValue) {
                    return true;
                }

                var parameters = decorator.FindPropertyRelative("parameters");
                if (originalDecorator.parameters.Count != parameters.arraySize)
                {
                    return true;
                }

                for (var k = 0; k < originalDecorator.parameters.Count; k++)
                {
                    var originalParameter = originalDecorator.parameters[k];
                    var newParameter = parameters.GetArrayElementAtIndex(k);
                    if (originalParameter.type != newParameter.FindPropertyRelative("type").stringValue)
                    {
                        return true;
                    }
                    if (!originalParameter.serializedValue.Equals(newParameter.FindPropertyRelative("serializedValue").stringValue)) {
                        return true;
                    }
                }
            }
            
            // TODO: originalProperty.items
        }

        return false;
    }

    private void DrawScriptBindingProperties(ScriptBinding binding) {
        EditorGUILayout.Space(5);

        var script = binding.m_script;
        var scriptPath = serializedObject.FindProperty("m_fileFullPath");
        var content = new GUIContent {
            text = "Script",
            tooltip = scriptPath.stringValue,
        };
        var newScript = EditorGUILayout.ObjectField(content, script, typeof(BinaryFile), true);
        if (newScript != script) {
            binding.m_script = (BinaryFile)newScript;
            scriptPath.stringValue = newScript == null ? "" : ((BinaryFile)newScript).m_path;
        }
        
        EditorGUILayout.Space(5);
    }

    private void DrawBinaryFileMetadata(ScriptBinding binding, SerializedProperty metadata) {
        EditorGUILayout.Space(5);
        var metadataProperties = metadata.FindPropertyRelative("properties");

        var propertyList = new List<SerializedProperty>();
        var indexDictionary = new Dictionary<string, int>();

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var property = metadataProperties.GetArrayElementAtIndex(i);
            propertyList.Add(property);
            indexDictionary.Add(binding.m_script.m_metadata.properties[i].name, i);
        }
        
        
        // Sort properties by order in non-serialized object
        propertyList.Sort((p1, p2) =>
            indexDictionary[p1.FindPropertyRelative("name").stringValue] > indexDictionary[p2.FindPropertyRelative("name").stringValue] ? 1 : -1
        );
        
        foreach (var prop in propertyList)
        {
            DrawCustomProperty(binding.m_script.m_metadata, prop);   
        }
    }

    // NOTE: This will probably change. Whole "decorators" structure will probably be redesigned.
    private bool HasDecorator(SerializedProperty modifiers, string modifier) {
        for (var i = 0; i < modifiers.arraySize; i++) {
            var element = modifiers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
            if (element.stringValue == modifier) {
                return true;
            }
        }
        return false;
    }

    private void DrawCustomProperty(LuauMetadata sourceMetadata, SerializedProperty property)
    {
        var bindingProperties = sourceMetadata.properties;
        
        var propName = property.FindPropertyRelative("name");
        // We have to find the property (they are not in the same order)
        var bindingProp = bindingProperties.Find((p) => p.name == propName.stringValue);
        var type = property.FindPropertyRelative("type");
        var objType = property.FindPropertyRelative("objectType");
        var items = property.FindPropertyRelative("items");
        var decorators = property.FindPropertyRelative("decorators");
        var value = property.FindPropertyRelative("serializedValue");
        var obj = property.FindPropertyRelative("serializedObject");
        var modified = property.FindPropertyRelative("modified");

        var propNameDisplay = ObjectNames.NicifyVariableName(propName.stringValue);
        var decoratorDictionary = GetDecorators(bindingProp);
        var guiContent = new GUIContent(propNameDisplay, GetTooltip("", decoratorDictionary));
        
        // Loop over styling decorators to display them in same order they were passed in
        foreach (var dec in bindingProp.decorators)
        {
            TryDrawDecorator(dec.name, dec.parameters);
        }
        
        if (decoratorDictionary.ContainsKey("HideInInspector"))
        {
            return;
        }

        switch (type.stringValue) {
            case "number":
                if (HasDecorator(decorators, "int")) {
                    DrawCustomIntProperty(guiContent, type, decoratorDictionary, value, modified);
                } else {
                    DrawCustomFloatProperty(guiContent, type, decoratorDictionary, value, modified);
                }
                break;
            case "string":
                DrawCustomStringProperty(guiContent, type, decoratorDictionary, value, modified);
                break;
            case "boolean" or "bool":
                DrawCustomBoolProperty(guiContent, type, decorators, value, modified);
                break;
            case "Vector2":
                DrawCustomVector2Property(guiContent, type, decorators, value, modified);
                break;
            case "Vector3":
                DrawCustomVector3Property(guiContent, type, decorators, value, modified);
                break;
            case "Vector4":
                DrawCustomVector4Property(guiContent, type, decorators, value, modified);
                break;
            case "Matrix4x4":
                DrawCustomMatrix4x4Property(sourceMetadata.name, propName.stringValue, guiContent, type, decorators, value, modified);
                break;
            case "LayerMask":
                DrawCustomLayerMaskProperty(guiContent, type, decorators, value, modified);
                break;
            case "Rect":
                DrawCustomRectProperty(guiContent, type, decorators, value, modified);
                break;
            case "object":
            case "Object":
                DrawCustomObjectProperty(guiContent, type, decorators, obj, objType, modified);
                break;
            case "Array":
                DrawCustomArrayProperty(guiContent, type, decorators, items);
                break;
            case "Color":
                DrawCustomColorProperty(guiContent, type, decorators, value, modified);
                break;
            case "Quaternion":
                DrawCustomQuaternionProperty(guiContent, type, decorators, value, modified);
                break;
            default:
                GUILayout.Label($"{propName.stringValue}: {type.stringValue} not yet supported");
                break;
        }
    }

    private void DrawCustomArrayProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty items) {
        if (EditorGUILayout.Foldout(true, guiContent)) {
            
        }
    }

    private void DrawCustomIntProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string,List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        int.TryParse(value.stringValue, out var currentValue);
        int newValue;
        if (modifiers.TryGetValue("Range", out var rangeProps))
        {
            var min = (float) rangeProps[0].value;
            var max = (float) rangeProps[0].value;
            newValue = EditorGUILayout.IntSlider(guiContent, currentValue, (int) min, (int) max);
        }
        else
        {
            newValue = EditorGUILayout.IntField(guiContent, currentValue);
        }
        
        if (modifiers.TryGetValue("Min", out var minParams))
        {
            newValue = Math.Max(Convert.ToInt32(minParams[0].value), newValue);
        }
        if (modifiers.TryGetValue("Max", out var maxParams))
        {
            newValue = Math.Min(Convert.ToInt32(maxParams[0].value), newValue);
        }
        
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }

    private void DrawCustomFloatProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        float.TryParse(value.stringValue, out var currentValue);
        float newValue;
        if (modifiers.TryGetValue("Range", out var rangeProps))
        {
            var min = Convert.ToSingle(rangeProps[0].value);
            var max = Convert.ToSingle(rangeProps[1].value);
            newValue = EditorGUILayout.Slider(guiContent, currentValue, min, max);
        }
        else
        {
            newValue = EditorGUILayout.FloatField(guiContent, currentValue);   
        }
        
        if (modifiers.TryGetValue("Min", out var minParams))
        {
            newValue = Math.Max(Convert.ToSingle(minParams[0].value), newValue);
        }
        if (modifiers.TryGetValue("Max", out var maxParams))
        {
            newValue = Math.Min(Convert.ToSingle(maxParams[0].value), newValue);
        }
        
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomStringProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        string newValue;
        
        // Flags for using text area
        var textAreaMaxLines = 3;
        var useTextArea = false;
        var displayTextAreaHorizontal = true;
        var displayFixedHeight = false;
        
        if (modifiers.TryGetValue("Multiline", out var multilineParams))
        {
            if (multilineParams.Count > 0) textAreaMaxLines = Convert.ToInt32(multilineParams[0].value);
            useTextArea = true;
            displayFixedHeight = true;
        }
        if (modifiers.ContainsKey("TextArea"))
        {
            useTextArea = true;
            displayTextAreaHorizontal = false;
            displayFixedHeight = false;
        }

        // Render flags for text area
        if (useTextArea)
        {
            if (displayTextAreaHorizontal) EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(guiContent);

            var style = EditorStyles.textArea;

            var maxHeight = style.lineHeight * textAreaMaxLines;
            if (displayFixedHeight) style.fixedHeight = maxHeight;
            newValue = EditorGUILayout.TextArea(value.stringValue, style, new []{ GUILayout.MaxHeight(maxHeight) });
            if (displayTextAreaHorizontal) EditorGUILayout.EndHorizontal();
        }
        else
        {
            newValue = EditorGUILayout.TextField(guiContent, value.stringValue);
        }
            
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomBoolProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = value.stringValue == "1";
        var newValue = EditorGUILayout.Toggle(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue ? "1" : "0";
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomRectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = JsonUtility.FromJson<Rect>(value.stringValue);
        var newValue = EditorGUILayout.RectField(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomLayerMaskProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        if (!int.TryParse(value.stringValue, out var currentValue))
        {
            currentValue = 0;
        }
        int newValue = EditorGUILayout.LayerField(guiContent, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomMatrix4x4Property(string scriptName, string propName, GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        // Check if matrix is already expanded
        if (!_openPropertyFoldouts.TryGetValue((scriptName, propName), out bool open))
        {
            open = true;
        }
        var currentValue = value.stringValue == "" ? new Matrix4x4() : JsonUtility.FromJson<Matrix4x4>(value.stringValue);
        var newState = EditorGUILayout.Foldout(open, guiContent, true);
        // If newState is opened then render matrix properties
        if (newState)
        {
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++)
                {
                    var newValue = EditorGUILayout.FloatField($"E{i}{j}", currentValue[i, j]);
                    if (newValue != currentValue[i, j])
                    {
                        currentValue[i, j] = newValue;
                        value.stringValue = JsonUtility.ToJson(currentValue);
                        modified.boolValue = true;
                    }
                }
            }
        }
        
        // Register new foldout state
        if (newState != open)
        {
            _openPropertyFoldouts[(scriptName, propName)] = newState;
        }
    }

    private void DrawCustomVector4Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = value.stringValue == "" ? new Vector4() : JsonUtility.FromJson<Vector4>(value.stringValue);
        var newValue = EditorGUILayout.Vector4Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    private void DrawCustomVector3Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = value.stringValue == "" ? new Vector3() : JsonUtility.FromJson<Vector3>(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomVector2Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = JsonUtility.FromJson<Vector2>(value.stringValue);
        var newValue = EditorGUILayout.Vector2Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomColorProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = JsonUtility.FromJson<Color>(value.stringValue);
        var newValue = EditorGUILayout.ColorField(guiContent, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomQuaternionProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = JsonUtility.FromJson<Quaternion>(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue.eulerAngles);
        if (newValue != currentValue.eulerAngles)
        {
            value.stringValue = JsonUtility.ToJson(Quaternion.Euler(newValue.x, newValue.y, newValue.z));
            modified.boolValue = true;
        }
    }

    private void DrawCustomObjectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty objType, SerializedProperty modified) {
        var currentObject = obj.objectReferenceValue;
        var t = objType.stringValue != "" ? TypeReflection.GetTypeFromString(objType.stringValue) : typeof(Object);
        var newObject = EditorGUILayout.ObjectField(guiContent, currentObject, t, true);
            
        if (newObject != currentObject) {
            obj.objectReferenceValue = newObject;
            modified.boolValue = true;
        }
    }
    
    // private Color ColorFromString(string value) {
    //     var values = value.Split(",");
    //     if (values.Length != 4) {
    //         return Color.white;
    //     }
    //     
    //     float.TryParse(values[0], out var r);
    //     float.TryParse(values[1], out var g);
    //     float.TryParse(values[2], out var b);
    //     float.TryParse(values[3], out var a);
    //
    //     return new Color(r, g, b, a);
    // }
    //
    // private string ColorToString(Color value) {
    //     var r = value.r.ToString(CultureInfo.InvariantCulture);
    //     var g = value.g.ToString(CultureInfo.InvariantCulture);
    //     var b = value.b.ToString(CultureInfo.InvariantCulture);
    //     var a = value.a.ToString(CultureInfo.InvariantCulture);
    //     return $"{r},{g},{b},{a}";
    // }

    private string StripAssetsFolder(string filePath) {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Bundles/");
        if (resourcesIndex >= 0) {
            filePath = filePath.Substring(resourcesIndex + "/Bundles/".Length);
        }
        return filePath;
    }

    /**
     * Returns a dictionary of (Decorator Name) -> (Parameters)
     */
    private Dictionary<string, List<LuauMetadataDecoratorValue>> GetDecorators(LuauMetadataProperty binding)
    {
        var decorators = binding.decorators;
        Dictionary<string, List<LuauMetadataDecoratorValue>> result = new();
        for (var i = 0; i < decorators.Count; i++)
        {
            var element = decorators[i];
            var decoratorName = element.name;
            var paramsProperty = element.parameters;
            result.Add(decoratorName, paramsProperty);
        }

        return result;
    }

    private void TryDrawDecorator(String name, List<LuauMetadataDecoratorValue> parameters)
    {
        switch (name)
        {
            case "Header":
                EditorGUILayout.Space();
                EditorGUILayout.LabelField((String) parameters[0].value, EditorStyles.boldLabel);
                return;
            case "Spacing":
                if (parameters.Count == 0)
                {
                    EditorGUILayout.Space();
                }
                else
                {
                    EditorGUILayout.Space(Convert.ToSingle(parameters[0].value));
                }
                return;
        }
    }
    
    private string GetTooltip(string comment, Dictionary<string, List<LuauMetadataDecoratorValue>> decorators)
    {
        // First try to grab tooltip from decorators 
        if (decorators.TryGetValue("Tooltip", out var tooltipProp))
        {
            var arrayIndex = tooltipProp[0];
            return arrayIndex.value.ToString();
        }
        // Fallback to using comment as tooltip
        return comment;
    }
}
#endif
