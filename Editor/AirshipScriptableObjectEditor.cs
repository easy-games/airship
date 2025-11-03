#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

[CustomEditor(typeof(AirshipSerializedLuauObject))]
public class AirshipSerializedLuauObjectEditor : UnityEditor.Editor {
    private AirshipEditor editor;
    public override void OnInspectorGUI() {
        AirshipSerializedLuauObject binding = (AirshipSerializedLuauObject)target;
        
        Type customEditorType = null;
        if (binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }
        
        if (customEditorType != null) {
            var componentEditor = AirshipCustomEditors.GetEditorForScriptableClass(binding, customEditorType, serializedObject);
            if (this.editor == null) this.editor = componentEditor;
            componentEditor.script = binding.GetAirshipType().Script;
            componentEditor.target = binding;
            componentEditor.OnInspectorGUI();
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}

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

        if (serializedObject.hasModifiedProperties) {
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    private void OnValidate() {
       
    }

    public override void OnInspectorGUI() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        
        Type customEditorType = null;
        if (binding.script != null && binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }

        var script = binding.script;
        EditorGUILayout.Space(5);

        GUI.enabled = script == null || script.scriptType != AirshipScriptType.ScriptableObject;
        var newScript = (AirshipScript) EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);
        GUI.enabled = true;

        if (newScript != script) {
            binding.script = newScript;
        }
              
        EditorGUILayout.Space(5);
        
        if (!AirshipCustomEditors.UseNewInspector) {
            EditorGUILayout.HelpBox("AirshipScriptableObject requires the new Editor API to be enabled", MessageType.Warning);
            return;
        }

        if (script == null) return;
        
        if (script.scriptType != AirshipScriptType.ScriptableObject) {
            EditorGUILayout.HelpBox("Script is not a ScriptableObject", MessageType.Warning);
            return;
        }
        
        Reconcile();
        
        if (customEditorType != null && binding.script != null) {
            var componentEditor = AirshipCustomEditors.GetEditorForScriptableObject(binding, customEditorType, serializedObject);
            if (this.editor == null) this.editor = componentEditor;
            componentEditor.script = binding.script;
            componentEditor.target = binding;
            componentEditor.OnInspectorGUI();
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        } else {
            EditorGUILayout.HelpBox("Could not find custom inspector", MessageType.Warning);
        }
    }
}
#endif