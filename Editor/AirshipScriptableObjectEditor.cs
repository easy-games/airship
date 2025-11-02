#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;


[CustomEditor(typeof(AirshipScriptableObject))]
public class AirshipScriptableObjectEditor : UnityEditor.Editor {
    private AirshipEditor editor;

    public void Reconcile() {
        var scriptableObject = (AirshipScriptableObject)target;
        var script = scriptableObject.script;
        
        if (script == null) return;
        if (script.m_metadata == null) return;
        
        var scriptMetadata = script.m_metadata;
        var componentMetadata = scriptableObject.metadata;

        if (scriptMetadata == null) return;
        componentMetadata.name = scriptMetadata.name;

        foreach (var scriptProperty in scriptMetadata.properties) {
            var componentProperty = componentMetadata.FindProperty(scriptProperty.name);
            if (componentProperty == null) {
                var element = scriptProperty.Clone();
                componentMetadata.properties.Add(element);
                componentProperty = element;
            } else {
                if (!componentProperty.HasSameTypesAs(scriptProperty)) {
                    componentProperty.ReconcileTypesWith(scriptProperty);
                }
                
                if (!componentProperty.HasSameItemsTypesAs(scriptProperty)) {
                    componentProperty.ReconcileItemsWith(scriptProperty);
                }
            }
            
            componentProperty.fileRef = scriptProperty.fileRef;
            componentProperty.refPath = scriptProperty.refPath;
        }
        
        List<LuauMetadataProperty> propertiesToRemove = null;
        var seenProperties = new HashSet<string>();
        foreach (var componentProperty in componentMetadata.properties) {
            var scriptProperty = scriptMetadata.FindProperty(componentProperty.name);
                
            if (scriptProperty == null || seenProperties.Contains(componentProperty.name)) {
                if (propertiesToRemove == null) {
                    propertiesToRemove = new List<LuauMetadataProperty>();
                }
                propertiesToRemove.Add(componentProperty);
            }
                
            seenProperties.Add(componentProperty.name);
        }
        
        
        if (propertiesToRemove != null) {
            foreach (var componentProperty in propertiesToRemove) {
                componentMetadata.properties.Remove(componentProperty);
            }
        }
    }
    
    private void OnValidate() {
       
    }

    public override void OnInspectorGUI() {
        Reconcile();
        if (!AirshipCustomEditors.UseNewInspector) return;
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        
        Type customEditorType = null;
        if (binding.script != null && binding.metadata != null) {
            // customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }

        if (customEditorType != null && binding.script != null) {
            var metadata = serializedObject.FindProperty("metadata");
            var metadataName = metadata.FindPropertyRelative("name");
            
            if (!string.IsNullOrEmpty(metadataName.stringValue)) {
                // var componentEditor = AirshipCustomEditors.GetEditorForComponent(binding, customEditorType, serializedObject);
                // if (this.editor == null) this.editor = componentEditor;
                // componentEditor.script = binding.script;
                // componentEditor.target = binding;
                // componentEditor.OnInspectorGUI();

            }
            
            serializedObject.ApplyModifiedProperties();
        } else {
            var script = binding.script;
            
            EditorGUILayout.Space(5);

            GUI.enabled = script == null;
            var newScript = (AirshipScript) EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);
            GUI.enabled = true;

            if (newScript != script) {
                binding.script = newScript;
            }
            
            EditorGUILayout.Space(5);
            
            var obj = new AirshipSerializedObject(binding);
            var properties = obj.GetProperties();
            foreach (var property in properties) {
                AirshipEditorGUI.PropertyField(property);
            }
            
            obj.ApplyModifiedProperties();
        }
    }
}
#endif