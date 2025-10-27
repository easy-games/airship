using System.Collections.Generic;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class AirshipReorderableArrayList : ReorderableList {
    public AirshipReorderableArrayList(
        AirshipSerializedObject serializedObject, 
        AirshipSerializedArray property,
        bool draggable = true,
        bool displayHeader = false,
        bool displayAddButton = true,
        bool displayRemoveButton = true
        ): base(
        serializedObject, 
        property, 
        draggable, 
        displayHeader, displayAddButton, displayRemoveButton) {}
}

/// <summary>
/// AirshipSerializedProperty and AirshipSerializedObject are classes for editing properties on airship objects in a completely generic way
/// that automatically handles undo, multi-object editing and Prefab overrides.
/// </summary>
public class AirshipSerializedObject {
    internal Dictionary<string, AirshipSerializedProperty> _propertyCache = new();
    internal SerializedObject serializedObject;
    internal SerializedProperty serializedMetadata => serializedObject.FindProperty("metadata");
    internal SerializedProperty serializedProperties => serializedMetadata.FindPropertyRelative("properties");
    internal SerializedProperty serializedName => serializedObject.FindProperty("name");
    internal LuauMetadata metadata { get; private set; }
    internal AirshipEditor editor { get; private set; }

    public AirshipType airshipType => AirshipBuildInfo.Instance.GetTypeByName(serializedName.stringValue);
    public AirshipComponent airshipComponent => (AirshipComponent)serializedObject.targetObject;

    internal AirshipSerializedObject() {}
    public AirshipSerializedObject(AirshipComponent component) => Update(null, new SerializedObject(component), component.metadata);
    
    [CanBeNull]
    internal AirshipSerializedObject prefabAsset {
        get {
            if (PrefabUtility.IsPartOfPrefabInstance(serializedObject.targetObject)) {
                var obj = new AirshipSerializedObject();
                var original = (AirshipComponent) PrefabUtility.GetCorrespondingObjectFromOriginalSource(serializedObject.targetObject);
                var serObj =
                    new SerializedObject(original);
                
                obj.Update(this.editor, serObj, original.metadata);
                return obj;
            } else {
                return null;
            }
        }
    }
    
    internal void Update(AirshipEditor currentEditor, SerializedObject currentSerializedObject, LuauMetadata currentMetadata) {
        _propertyCache.Clear();
        serializedObject = currentSerializedObject;
        editor = currentEditor;
        metadata = currentMetadata;
    }
    
    /// <summary>
    /// Finds the airship property with the given name
    /// </summary>
    /// <param name="targetPropertyName">The name of the property (should match the variable in TypeScript)</param>
    /// <returns>The AirshipProperty, if it exists</returns>
    public AirshipSerializedProperty FindAirshipProperty(string targetPropertyName) {
        // if (_propertyCache.TryGetValue(targetPropertyName, out var cachedProperty)) {
        //     return cachedProperty;
        // }
        
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            var propertyName = property.FindPropertyRelative("name").stringValue;
            if (propertyName == targetPropertyName) {
                var propertyMetadata = metadata.FindProperty(targetPropertyName);
               
                var airshipProperty = new AirshipSerializedProperty(property, propertyMetadata, this.editor);
                // _propertyCache.Add(targetPropertyName, airshipProperty);
                return airshipProperty;
            }
        }

        return default;
    }
    
    public IReadOnlyList<AirshipSerializedProperty> GetProperties() {
        var propertyList = new List<AirshipSerializedProperty>();
        var indexDictionary = new Dictionary<string, int>();

        for (var i = 0; i < this.serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            var propertyName = property.FindPropertyRelative("name").stringValue;
            
            var bindingPropertyIndex = metadata.properties.FindIndex(p => p.name == propertyName);
            if (bindingPropertyIndex == -1) continue;
            var bindingProperty = metadata.properties[bindingPropertyIndex];
            
            propertyList.Add(new AirshipSerializedProperty(property, bindingProperty, editor));
            indexDictionary.Add(bindingProperty.name, bindingPropertyIndex);
        }

        propertyList.Sort((p1, p2) => {
            return indexDictionary[p1.name] > indexDictionary[p2.name] ? 1 : -1;
        });
        
        return propertyList;
    }
    
    public static implicit operator SerializedObject(AirshipSerializedObject value) {
        return value.serializedObject;
    }
    
    public static explicit operator AirshipSerializedObject(AirshipComponent component) {
        var obj = new AirshipSerializedObject();
        obj.Update(null, new SerializedObject(component), component.metadata);
        return obj;
    }

    public void ApplyModifiedProperties() {
        serializedObject.ApplyModifiedProperties();
    }
}