#if UNITY_EDITOR

using System;
using System.Globalization;
using Luau;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ScriptBinding))]
public class ScriptBindingEditor : Editor
{
    private bool _showScriptBindingProperties = true;
    private bool _showAirshipBehaviourProperties = true;
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ScriptBinding binding = (ScriptBinding)target;

        _showScriptBindingProperties = EditorGUILayout.BeginFoldoutHeaderGroup(_showScriptBindingProperties, "Script Binding");
        if (_showScriptBindingProperties)
        {
            DrawScriptBindingProperties(binding);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (binding.m_binaryFile != null)
        {
            _showAirshipBehaviourProperties = EditorGUILayout.BeginFoldoutHeaderGroup(_showAirshipBehaviourProperties, "Airship Behaviour Properties");
            if (_showAirshipBehaviourProperties)
            {
                DrawBinaryFileMetadata(binding, binding.m_binaryFile);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptBindingProperties(ScriptBinding binding)
    {
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
        
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Select script file", "Assets", new string[] { "Airship scripts", "lua" });
            if (!string.IsNullOrEmpty(path))
            {
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
        // GUILayout.Label("Example: shared/resources/ts/main");
        GUILayout.Label($"Asset bundle path: {(string.IsNullOrEmpty(binding.m_fileFullPath) ? "(None)" : binding.m_fileFullPath)}");

        EditorGUILayout.Space(5);

        EditorGUILayout.Toggle("Error", binding.m_error, EditorStyles.radioButton);
        EditorGUILayout.Toggle("Yielded", binding.m_yielded, EditorStyles.radioButton);
        
        EditorGUILayout.Space(5);
    }

    private void DrawBinaryFileMetadata(ScriptBinding binding, BinaryFile binaryFile)
    {
        EditorGUILayout.Space(5);
        var metadata = serializedObject.FindProperty("m_metadata");
        var metadataProperties = metadata.FindPropertyRelative("properties");
        for (var i = 0; i < metadataProperties.arraySize; i++)
        {
            var property = metadataProperties.GetArrayElementAtIndex(i);
            DrawCustomProperty(property);
        }
    }

    // NOTE: This will probably change. Whole "modifiers" structure will probably be redesigned.
    private bool HasModifier(SerializedProperty modifiers, string modifier)
    {
        for (var i = 0; i < modifiers.arraySize; i++)
        {
            var element = modifiers.GetArrayElementAtIndex(i);
            if (element.stringValue == modifier)
            {
                return true;
            }
        }
        return false;
    }

    private void DrawCustomProperty(SerializedProperty property)
    {
        var propName = property.FindPropertyRelative("name");
        var type = property.FindPropertyRelative("type");
        var modifiers = property.FindPropertyRelative("modifiers");
        var value = property.FindPropertyRelative("serializedValue");

        var propNameDisplay = ObjectNames.NicifyVariableName(propName.stringValue);

        switch (type.stringValue)
        {
            case "number":
                if (HasModifier(modifiers, "int"))
                {
                    DrawCustomIntProperty(propNameDisplay, type, modifiers, value);
                }
                else
                {
                    DrawCustomFloatProperty(propNameDisplay, type, modifiers, value);
                }
                break;
            case "string":
                DrawCustomStringProperty(propNameDisplay, type, modifiers, value);
                break;
            case "boolean" or "bool":
                DrawCustomBoolProperty(propNameDisplay, type, modifiers, value);
                break;
            default:
                GUILayout.Label($"Unsupported type for property {propName.stringValue}: {type.stringValue}");
                break;
        }
    }

    private void DrawCustomIntProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value)
    {
        int.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.IntField(propName, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void DrawCustomFloatProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value)
    {
        float.TryParse(value.stringValue, out var currentValue);
        var newValue = EditorGUILayout.FloatField(propName, currentValue);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue)
        {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
        }
    }
    
    private void DrawCustomStringProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value)
    {
        var newValue = EditorGUILayout.TextField(propName, value.stringValue);
        if (newValue != value.stringValue)
        {
            value.stringValue = newValue;
        }
    }
    
    private void DrawCustomBoolProperty(string propName, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value)
    {
        var currentValue = value.stringValue == "1";
        var newValue = EditorGUILayout.Toggle(propName, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = newValue ? "1" : "0";
        }
    }

    private string StripAssetsFolder(string filePath)
    {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Bundles/");
        if (resourcesIndex >= 0)
        {
            filePath = filePath.Substring(resourcesIndex + "/Bundles/".Length);
        }
        return filePath;
    }
}
#endif
