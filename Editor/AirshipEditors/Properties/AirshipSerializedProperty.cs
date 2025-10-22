using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;


/// <summary>
/// AirshipSerializedProperty and AirshipSerializedObject are classes for editing properties on airship objects in a completely generic way
/// that automatically handles undo, multi-object editing and Prefab overrides.
/// </summary>
public class AirshipSerializedProperty : AirshipSerializedValue {
    internal SerializedProperty serializedProperty;
    internal SerializedProperty serializedItems { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }

    public bool isArray => serializedType.stringValue == "Array";
    internal bool valid { get; set; } = true;
    
    /// <summary>
    /// Whether or not this property was modified in a prefab
    /// </summary>
    public bool prefabOverride {
        get => this.serializedValue.prefabOverride || this.serializedObjectValue.prefabOverride || (isArray && array.prefabOverride);
    }
    
    /// <summary>
    /// If this property is an array, will have the size of the items in the array
    /// </summary>
    public int arraySize {
        get {
            if (!isArray) return 0;
            return serializedItems.FindPropertyRelative("serializedItems").arraySize;
        }
    }

    /// <summary>
    /// If the property is an array, will return the serialized array
    /// </summary>
    public AirshipSerializedArray array {
        get {
            if (!isArray) return null;
            
            UpdateProperty();
            return new AirshipSerializedArray(
                this, 
                serializedItems.FindPropertyRelative("serializedItems"), 
                serializedItems.FindPropertyRelative("objectRefs"));
        }
    }
    
    public AirshipSerializedProperty(SerializedProperty property, LuauMetadataProperty metadata, AirshipEditor editor) {
        serializedProperty = property;
        UpdateProperty();
        propertyMetadata = metadata;
        decorators = metadata.GetDecorators();
        this.editor = editor;
    }
    
    internal void UpdateProperty() {
        serializedName = serializedProperty.FindPropertyRelative("name");
        
        serializedType = serializedProperty.FindPropertyRelative("type");
        serializedValue = serializedProperty.FindPropertyRelative("serializedValue");
        serializedModified = serializedProperty.FindPropertyRelative("modified");
        
        serializedObjectType = serializedProperty.FindPropertyRelative("objectType");
        serializedObjectValue = serializedProperty.FindPropertyRelative("serializedObject");
        
        serializedItems = serializedProperty.FindPropertyRelative("items");
        
        serializedRef = serializedProperty.FindPropertyRelative("refPath");
        serializedFileRef = serializedProperty.FindPropertyRelative("fileRef");
    }

    internal Object prefab => PrefabUtility.GetPrefabInstanceHandle(serializedProperty.serializedObject.targetObject);
    internal Object prefabInstanceRoot => PrefabUtility.GetNearestPrefabInstanceRoot(serializedProperty.serializedObject.targetObject);
    
    /// <summary>
    /// Revert the override for this property
    /// </summary>
    /// <param name="interactionMode"></param>
    internal void RevertPropertyOverride(InteractionMode interactionMode) {
        if (!this.prefabOverride) return;

        if (isArray) this.array.RevertPropertyOverride(interactionMode);
        PrefabUtility.RevertPropertyOverride(this.serializedValue, interactionMode);
        PrefabUtility.RevertPropertyOverride(this.serializedObjectValue, interactionMode);
    }

    internal void ApplyPropertyOverride(InteractionMode interactionMode) {
        var assetPath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(serializedProperty.serializedObject.targetObject);

        if (isArray) this.array.ApplyPropertyOverride(assetPath, interactionMode);
        PrefabUtility.ApplyPropertyOverride(this.serializedValue, assetPath, InteractionMode.UserAction);
    }
    
    internal void ResetToDefault() {
        if (isObject) serializedObjectValue.objectReferenceValue = null;
        if (isArray) array.ResetToDefault();
        
        propertyMetadata.SetDefaultAsValue();
        propertyMetadata.modified = false;
        isModified = false;
        serializedProperty.serializedObject.ApplyModifiedProperties();
    }
}