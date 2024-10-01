#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using Object = System.Object;

[CustomEditor(typeof(AirshipRuntimeScript))]
public class AirshipRuntimeScriptEditor : UnityEditor.Editor {

    private bool debugging = false;
    
    public override void OnInspectorGUI() {
        serializedObject.Update();

        AirshipRuntimeScript binding = (AirshipRuntimeScript)target;
        if (binding.scriptFile == null && !string.IsNullOrEmpty(binding.m_fileFullPath)) {
            if (binding.scriptFile == null) {
                Debug.LogWarning($"Failed to load script asset: {binding.m_fileFullPath}");
                EditorGUILayout.HelpBox("Missing reference. This is likely from renaming a script.\n\nOld path: " + binding.m_fileFullPath.Replace("Assets/Bundles/", ""), MessageType.Warning);
            }
        }

        DrawScriptBindingProperties(binding);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptBindingProperties(AirshipRuntimeScript binding) {
        EditorGUILayout.Space(5);

        var script = binding.scriptFile;
        var scriptPath = serializedObject.FindProperty("m_fileFullPath");
        var content = new GUIContent {
            text = "Script",
            tooltip = scriptPath.stringValue,
        };
        
        var newScript = EditorGUILayout.ObjectField(content, script, typeof(AirshipScript), true);
        if (newScript != script) {
            binding.scriptFile = (AirshipScript)newScript;
            scriptPath.stringValue = newScript == null ? "" : ((AirshipScript)newScript).assetPath;
            serializedObject.ApplyModifiedProperties();
        }

        GUI.enabled = true;
        
        EditorGUILayout.Space(5);
    }
}
#endif
