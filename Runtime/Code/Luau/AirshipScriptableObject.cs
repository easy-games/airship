using System;
using System.Collections.Generic;
using Luau;
using UnityEditor;
using UnityEngine;


[CreateAssetMenu(menuName = "Create AirshipScriptableObject", fileName = "AirshipScriptableObject", order = 0)]
public class AirshipScriptableObject : ScriptableObject, ISerializationCallbackReceiver {
    public AirshipScript script;
#if !AIRSHIP_INTERNAL
    [HideInInspector]
#endif
    public LuauMetadata metadata;

    public void OnBeforeSerialize() {
        
    }

    public void OnAfterDeserialize() {
        if (script == null) {
            metadata = default;
        } else if (script.m_metadata != null) {
            Debug.Log("should reconcile");
            Reconcile();
        }
    }

    private void OnValidate() {
        Reconcile();
    }

    public void Reconcile() {
        if (script == null) return;
        if (script.m_metadata == null) return;
        
        var scriptMetadata = script.m_metadata;
        var componentMetadata = metadata;

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
}