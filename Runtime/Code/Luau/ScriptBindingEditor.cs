using System;
using Luau;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
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
        binding.m_fileFullPath = StripAssetsFolder(inputPath);
        binding.m_assetPath = inputPath;
        
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Select script file", "Assets", new string[] { "Airship scripts", "lua" });
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = FileUtil.GetProjectRelativePath(path);
                binding.m_fileFullPath = StripAssetsFolder(relativePath);
                binding.m_assetPath = relativePath;
                Undo.RecordObject(binding, "Set Script File");
                EditorUtility.SetDirty(binding);
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
        var metadata = binaryFile.m_metadata;
        foreach (var property in metadata.properties)
        {
            DrawCustomProperty(binding, property);
        }
    }

    private void DrawCustomProperty(ScriptBinding binding, LuauMetadataProperty<object> property)
    {
        var serializedProperty = binding.m_metadata.FindProperty<object>(property.name);
        if (serializedProperty == null) return;

        var dirty = false;
        
        switch (property.type)
        {
            case "number":
                if (property.HasModifier("int"))
                {
                    var currentValue = Convert.ToInt32(serializedProperty.value);
                    var newValue = EditorGUILayout.IntField(property.name, currentValue);
                    if (newValue != currentValue)
                    {
                        serializedProperty.value = newValue;
                        dirty = true;
                    }
                }
                else
                {
                    var currentValue = Convert.ToSingle(serializedProperty.value);
                    var newValue = EditorGUILayout.FloatField(property.name, currentValue);
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (newValue != currentValue)
                    {
                        serializedProperty.value = newValue;
                        dirty = true;
                    }
                }
                break;
        }

        if (dirty)
        {
            Undo.RecordObject(binding, "Set Custom Property");
            EditorUtility.SetDirty(binding);
        }
    }

    private string StripAssetsFolder(string filePath)
    {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Resources/");
        if (resourcesIndex >= 0)
        {
            filePath = filePath.Substring(resourcesIndex + "/Resources/".Length);
        }
        return filePath;
    }
}
#endif