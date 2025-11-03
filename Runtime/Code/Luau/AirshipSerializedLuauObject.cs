using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;

[Serializable]
public class AirshipSerializedLuauObject : ScriptableObject {
    public string fileRef;
    public string type;
    public LuauMetadata metadata;

#if UNITY_EDITOR
    public void Reconcile(LuauMetadata otherMetadata) {
        if (metadata.properties == null) metadata.properties = new List<LuauMetadataProperty>();
        metadata.name = otherMetadata.name;
        
        foreach (var scriptProperty in otherMetadata.properties) {
            var componentProperty = metadata.FindProperty(scriptProperty.name);
            if (componentProperty == null) {
                var element = scriptProperty.Clone();
                metadata.properties.Add(element);
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
        foreach (var componentProperty in metadata.properties) {
            var scriptProperty = otherMetadata.FindProperty(componentProperty.name);
                
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
                metadata.properties.Remove(componentProperty);
            }
        }
    }
#endif
}