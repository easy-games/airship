#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using System.Globalization;
using System;
using Luau;
using Object = System.Object;

[CustomEditor(typeof(ScriptBinding))]
public class ScriptBindingEditor : Editor {
    public override void OnInspectorGUI() {
        serializedObject.Update();

        ScriptBinding binding = (ScriptBinding)target;

        if (binding.m_script == null && !string.IsNullOrEmpty(binding.m_fileFullPath)) {
            // Attempt to find the script based on the filepath:
            Debug.Log("Attempting to reconcile script asset from path...");
            
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
            var originalProperty = originalMetadataProperties[i];
            
            var modified = metadataProperty.FindPropertyRelative("modified");
            if (modified.boolValue) continue;
            
            var serializedValue = metadataProperty.FindPropertyRelative("serializedValue");
            if (serializedValue.stringValue != originalProperty.serializedValue) {
                serializedValue.stringValue = originalProperty.serializedValue;
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

            for (var j = 0; j < decorators.arraySize; j++) {
                if (originalProperty.decorators[j] != decorators.GetArrayElementAtIndex(i).stringValue) {
                    return true;
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
        var items = property.FindPropertyRelative("items");
        var decorators = property.FindPropertyRelative("decorators");
        var value = property.FindPropertyRelative("serializedValue");
        var obj = property.FindPropertyRelative("serializedObject");
        var modified = property.FindPropertyRelative("modified");

        var propNameDisplay = ObjectNames.NicifyVariableName(propName.stringValue);

        switch (type.stringValue) {
            case "number":
                if (HasDecorator(decorators, "int")) {
                    DrawCustomIntProperty(propNameDisplay, type, decorators, value, modified);
                } else {
                    DrawCustomFloatProperty(propNameDisplay, type, decorators, value, modified);
                }
                break;
            case "string":
                DrawCustomStringProperty(propNameDisplay, type, decorators, value, modified);
                break;
            case "boolean" or "bool":
                DrawCustomBoolProperty(propNameDisplay, type, decorators, value, modified);
                break;
            case "Vector3":
                DrawCustomVector3Property(propNameDisplay, type, decorators, value, modified);
                break;
            case "object":
                DrawCustomObjectProperty(propNameDisplay, type, decorators, obj, objType, modified);
                break;
            case "Array":
                DrawCustomArrayProperty(propNameDisplay, type, decorators, items);
                break;
            default:
                GUILayout.Label($"{propName.stringValue}: {type.stringValue} not yet supported");
                break;
        }
    }

    private void DrawCustomArrayProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty items) {
        if (EditorGUILayout.Foldout(true, propName)) {
            
        }
    }

    private void DrawCustomIntProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        int.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.IntField(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }

    private void DrawCustomFloatProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        float.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.FloatField(propName, currentValue);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomStringProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var newValue = EditorGUILayout.TextField(propName, value.stringValue);
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomBoolProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = value.stringValue == "1";
        var newValue = EditorGUILayout.Toggle(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue ? "1" : "0";
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomVector3Property(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = Vector3FromString(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(propName, currentValue);
        if (newValue != currentValue) {
            value.stringValue = Vector3ToString(newValue);
            modified.boolValue = true;
        }
    }

    private void DrawCustomObjectProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty objType, SerializedProperty modified) {
        var currentObject = obj.objectReferenceValue;
        var t = TypeReflection.GetTypeFromString(objType.stringValue);
        var newObject = EditorGUILayout.ObjectField(propName, currentObject, t, true);
        if (newObject != currentObject) {
            obj.objectReferenceValue = newObject;
            modified.boolValue = true;
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
