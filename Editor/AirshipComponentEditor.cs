#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Code.Luau;
using Editor.AirshipPropertyEditor;
using JetBrains.Annotations;
using Luau;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using Object = System.Object;

[CustomEditor(typeof(AirshipComponent))]
public class ScriptBindingEditor : AirshipEditor<AirshipComponent> {
    private void DrawScriptBindingProperties(AirshipComponent binding) {
        EditorGUILayout.Space(5);

        var script = binding.scriptFile;
        var scriptPath = serializedObject.FindProperty("m_fileFullPath");
        var content = new GUIContent {
            text = "Script",
            tooltip = scriptPath.stringValue,
        };

        if (binding.scriptFile != null && (binding.m_metadata != null || Application.isPlaying)) {
            GUI.enabled = false;
        }
        
        var newScript = EditorGUILayout.ObjectField(content, script, typeof(AirshipScript), true);
        if (newScript != script) {
            binding.scriptFile = (AirshipScript)newScript;
            scriptPath.stringValue = newScript == null ? "" : ((AirshipScript)newScript).assetPath;
            serializedObject.ApplyModifiedProperties();
        }

        GUI.enabled = true;
        
        if (newScript == null) {
            EditorGUILayout.Space(5);
            
            var rect = GUILayoutUtility.GetLastRect();
            var style = new GUIStyle(EditorStyles.miniButton);
            style.padding = new RectOffset(50, 50, 0, 0);
            style.fixedHeight = 25;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Airship Component", style)) {
                AirshipComponentDropdown dd = new AirshipComponentDropdown(new AdvancedDropdownState(), (binaryFile) => {
                    binding.SetScript(binaryFile);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serializedObject.targetObject);
                });
                dd.Show(rect, 300);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space(5);
    }
    
    public override void OnInspectorGUI() {
        serializedObject.Update();

        AirshipComponent binding = (AirshipComponent)target;

        if (binding.scriptFile == null && !string.IsNullOrEmpty(binding.m_fileFullPath)) {
            // Debug.Log("Setting Script File from Path: " + binding.m_fileFullPath);
            // binding.SetScriptFromPath(binding.m_fileFullPath, LuauContext.Game);
            if (binding.scriptFile == null) {
                Debug.LogWarning($"Failed to load script asset: {binding.m_fileFullPath}");
                EditorGUILayout.HelpBox("Missing reference. This is likely from renaming a script.\n\nOld path: " + binding.m_fileFullPath.Replace("Assets/Bundles/", ""), MessageType.Warning);
            }
        }

        DrawScriptBindingProperties(binding);

        if (binding.scriptFile != null && binding.scriptFile.m_metadata != null) {
            if (ShouldReconcile(binding)) {
                binding.ReconcileMetadata();
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            CheckDefaults(binding);
        }

        DrawProperties(binding, binding.scriptFile);
    }

    protected override void CheckDefaults(AirshipComponent binding) {
        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.scriptFile.m_metadata.properties;

        var setDefault = false;

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var propName = metadataProperty.FindPropertyRelative("name").stringValue;
            var originalProperty = originalMetadataProperties.Find((p) => p.name == propName);
            
            var modified = metadataProperty.FindPropertyRelative("modified");
            if (modified.boolValue) {
                bool HaveTypesChanged() {
                    if (originalProperty.type != metadataProperty.FindPropertyRelative("type").stringValue) return true;
                    if (originalProperty.objectType != metadataProperty.FindPropertyRelative("objectType").stringValue)
                        return true;

                    var itemsProperty = metadataProperty.FindPropertyRelative("items");
                    if (originalProperty.items.type != itemsProperty.FindPropertyRelative("type").stringValue)
                        return true;
                    if (originalProperty.items.objectType != itemsProperty.FindPropertyRelative("objectType").stringValue)
                        return true;
                    return false;
                }
                
                // Verify object type hasn't changed. If it has overwrite with defaults anyway
                if (!HaveTypesChanged()) continue;
            }
            
            
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

    protected override bool ShouldReconcile(AirshipComponent binding) {
        if (binding.m_metadata == null || binding.scriptFile.m_metadata == null) return false;

        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.scriptFile.m_metadata?.properties;

        if (metadataProperties.arraySize != originalMetadataProperties.Count) {
            return true;
        }

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var metadataName = metadataProperty.FindPropertyRelative("name").stringValue;
            // We could use originalMetadataProperties[i] (faster), but order might be out of sync. We don't correct
            // for out of sync order in ReconcileMetadata right now.
            var originalProperty = originalMetadataProperties.Find(property => property.name == metadataName);
            if (originalProperty == null) {
                return true;
            }

            // if (originalProperty.name != metadataName) {
            //     return true;
            // }

            if (originalProperty.type != metadataProperty.FindPropertyRelative("type").stringValue) {
                return true;
            }
            
            if (originalProperty.objectType != metadataProperty.FindPropertyRelative("objectType").stringValue) {
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
            if (originalProperty.items.type != metadataProperty.FindPropertyRelative("items").FindPropertyRelative("type").stringValue) {
                return true;
            }
            if (originalProperty.items.objectType != metadataProperty.FindPropertyRelative("items").FindPropertyRelative("objectType").stringValue) {
                return true;
            }
        }

        return false;
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
