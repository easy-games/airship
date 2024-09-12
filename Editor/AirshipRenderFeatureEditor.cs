using System;
using System.Collections.Generic;
using Code.Luau;
using Editor.AirshipPropertyEditor;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor {
    [CustomEditor(typeof(AirshipScriptableRendererFeature))]
    public class AirshipRenderFeatureEditor : AirshipEditor<AirshipScriptableRendererFeature> {
        protected override bool ShouldReconcile(AirshipScriptableRendererFeature binding) {
            if (binding.m_metadata == null || binding.script.m_metadata == null) return false;

            var metadata = serializedObject.FindProperty("m_metadata");
            
            var metadataProperties = metadata.FindPropertyRelative("properties");
            var originalMetadataProperties = binding.script.m_metadata?.properties;

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

        protected override void CheckDefaults(AirshipScriptableRendererFeature binding) {
            var metadata = serializedObject.FindProperty("m_metadata");
        
            var metadataProperties = metadata.FindPropertyRelative("properties");
            var originalMetadataProperties = binding.script.m_metadata.properties;

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

        protected override void ReconcileMetadata(AirshipScriptableRendererFeature binding) {
            LuauMetadata.ReconcileMetadata(binding.script.m_metadata, binding.m_metadata);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            var feature = (AirshipScriptableRendererFeature)target;
            
            var content = new GUIContent {
                text = "Script",
                // tooltip = scriptPath.stringValue,
            };
            var newScript = EditorGUILayout.ObjectField(content, feature.script, typeof(AirshipRenderPassScript), false);
            if (newScript != feature.script) {
                feature.script = (AirshipRenderPassScript)newScript;
                serializedObject.ApplyModifiedProperties();
            }
            
            // GUI.enabled = true;
            
            if (feature.script == null) {
                EditorGUILayout.HelpBox("This is an experimental feature for Airship and may have bugs or unintended side-effects", MessageType.Warning);
                return;
            }

            DrawProperties(feature, feature.script);
        }
    }
}