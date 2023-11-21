#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using System.Globalization;
using System;
using Object = System.Object;

[CustomEditor(typeof(ScriptBinding))]
public class ScriptBindingEditor : Editor {
    private bool _showScriptBindingProperties = true;
    private bool _showAirshipBehaviourProperties = true;
    
    public override void OnInspectorGUI() {
        serializedObject.Update();

        ScriptBinding binding = (ScriptBinding)target;

        _showScriptBindingProperties = EditorGUILayout.BeginFoldoutHeaderGroup(_showScriptBindingProperties, "Script Binding");
        if (_showScriptBindingProperties) {
            DrawScriptBindingProperties(binding);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (binding.m_binaryFile != null) {
            var metadata = serializedObject.FindProperty("m_metadata");
            var metadataName = metadata.FindPropertyRelative("name");
            _showAirshipBehaviourProperties = EditorGUILayout.BeginFoldoutHeaderGroup(_showAirshipBehaviourProperties, metadataName.stringValue);
            if (_showAirshipBehaviourProperties)
            {
                DrawBinaryFileMetadata(binding, metadata);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptBindingProperties(ScriptBinding binding) {
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
     
        var style = new GUIStyle(EditorStyles.textField);
        style.alignment = TextAnchor.MiddleRight;
        
        var inputPath = EditorGUILayout.TextField("Script File", binding.m_assetPath, style, GUILayout.ExpandWidth(true));
        var fullPath = serializedObject.FindProperty("m_fileFullPath");
        var assetPath = serializedObject.FindProperty("m_assetPath");
        // binding.m_fileFullPath = StripAssetsFolder(inputPath);
        // binding.m_assetPath = inputPath;
        fullPath.stringValue = StripAssetsFolder(inputPath);
        assetPath.stringValue = inputPath;
        
        if (GUILayout.Button("...", GUILayout.Width(30))) {
            string path = EditorUtility.OpenFilePanelWithFilters("Select script file", "Assets", new string[] { "Airship scripts", "lua" });
            if (!string.IsNullOrEmpty(path)) {
                string relativePath = FileUtil.GetProjectRelativePath(path);
                // binding.m_fileFullPath = StripAssetsFolder(relativePath);
                // binding.m_assetPath = relativePath;
                // Undo.RecordObject(binding, "Set Script File");
                // EditorUtility.SetDirty(binding);
                fullPath.stringValue = StripAssetsFolder(relativePath);
                assetPath.stringValue = relativePath;
            }
        }
        EditorGUILayout.EndHorizontal();

        //Add an edit button
        if (GUILayout.Button("Edit")) {
            // Get the path from your serialized property or however you are obtaining the path
            string fullPathString = fullPath.stringValue;

            fullPathString = Path.Combine("Assets/Bundles", fullPathString);

            // Check if the file exists before trying to open it
            if (File.Exists(fullPathString)) {
                // Use reflection to specifically find the overload of OpenFileAtLineExternal that we want
                System.Type T = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.InternalEditorUtility");
                MethodInfo method = T.GetMethod("OpenFileAtLineExternal", new Type[] { typeof(string), typeof(int) });

                if (method != null) {
                    // Invoke the method with the path and a line number (0 means open without a specific line)
                    method.Invoke(null, new object[] { fullPathString, -1 }); // -1 to open without highlighting any specific line
                } else {
                    Debug.LogError("Could not find method: OpenFileAtLineExternal");
                }
            } else {
                Debug.LogError("File does not exist: " + fullPathString);
            }
        }


        // GUILayout.Label("Example: shared/resources/ts/main");
        GUILayout.Label($"Asset bundle path: {(string.IsNullOrEmpty(binding.m_fileFullPath) ? "(None)" : binding.m_fileFullPath)}");

        EditorGUILayout.Space(5);

        EditorGUILayout.Toggle("Error", binding.m_error, EditorStyles.radioButton);
        EditorGUILayout.Toggle("Yielded", binding.m_yielded, EditorStyles.radioButton);
        
        EditorGUILayout.Space(5);
    }

    private void DrawBinaryFileMetadata(ScriptBinding binding, SerializedProperty metadata) {
        EditorGUILayout.Space(5);
        var metadataProperties = metadata.FindPropertyRelative("properties");
        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var property = metadataProperties.GetArrayElementAtIndex(i);
            DrawCustomProperty(property);
        }
    }

    // NOTE: This will probably change. Whole "decorators" structure will probably be redesigned.
    private bool HasDecorator(SerializedProperty modifiers, string modifier) {
        for (var i = 0; i < modifiers.arraySize; i++) {
            var element = modifiers.GetArrayElementAtIndex(i);
            if (element.stringValue == modifier) {
                return true;
            }
        }
        return false;
    }

    private void DrawCustomProperty(SerializedProperty property) {
        var propName = property.FindPropertyRelative("name");
        var type = property.FindPropertyRelative("type");
        var objType = property.FindPropertyRelative("objectType");
        var decorators = property.FindPropertyRelative("decorators");
        var value = property.FindPropertyRelative("serializedValue");
        var obj = property.FindPropertyRelative("serializedObject");

        var propNameDisplay = ObjectNames.NicifyVariableName(propName.stringValue);

        switch (type.stringValue) {
            case "number":
                if (HasDecorator(decorators, "int")) {
                    DrawCustomIntProperty(propNameDisplay, type, decorators, value);
                } else {
                    DrawCustomFloatProperty(propNameDisplay, type, decorators, value);
                }
                break;
            case "string":
                DrawCustomStringProperty(propNameDisplay, type, decorators, value);
                break;
            case "boolean" or "bool":
                DrawCustomBoolProperty(propNameDisplay, type, decorators, value);
                break;
            case "Vector3":
                DrawCustomVector3Property(propNameDisplay, type, decorators, value);
                break;
            case "object":
                DrawCustomObjectProperty(propNameDisplay, type, decorators, obj, objType);
                break;
            default:
                GUILayout.Label($"Unsupported type for property {propName.stringValue}: {type.stringValue}");
                break;
        }
    }

    private void DrawCustomIntProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value) {
        int.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.IntField(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void DrawCustomFloatProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value) {
        float.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.FloatField(propName, currentValue);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
        }
    }
    
    private void DrawCustomStringProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value) {
        var newValue = EditorGUILayout.TextField(propName, value.stringValue);
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
        }
    }
    
    private void DrawCustomBoolProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value) {
        var currentValue = value.stringValue == "1";
        var newValue = EditorGUILayout.Toggle(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue ? "1" : "0";
        }
    }
    
    private void DrawCustomVector3Property(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value) {
        var currentValue = Vector3FromString(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = Vector3ToString(newValue);
        }
    }

    private void DrawCustomObjectProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty objType) {
        var currentObject = obj.objectReferenceValue;
        var t = TypeReflection.Instance.GetTypeFromString(objType.stringValue);
        var newObject = EditorGUILayout.ObjectField(propName, currentObject, t, true);
        if (newObject != currentObject) {
            obj.objectReferenceValue = newObject;
        }
    }

    private Vector3 Vector3FromString(string value) {
        var values = value.Split(",");
        if (values.Length != 3) {
            return Vector3.zero;
        }
        
        float.TryParse(values[0], out var x);
        float.TryParse(values[1], out var y);
        float.TryParse(values[2], out var z);

        return new Vector3(x, y, z);
    }

    private string Vector3ToString(Vector3 value) {
        var x = value.x.ToString(CultureInfo.InvariantCulture);
        var y = value.y.ToString(CultureInfo.InvariantCulture);
        var z = value.z.ToString(CultureInfo.InvariantCulture);
        return $"{x},{y},{z}";
    }

    private string StripAssetsFolder(string filePath) {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Bundles/");
        if (resourcesIndex >= 0) {
            filePath = filePath.Substring(resourcesIndex + "/Bundles/".Length);
        }
        return filePath;
    }
}
#endif
