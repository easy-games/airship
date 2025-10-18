using System;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

public class AirshipSerializedProperty : AirshipSerializedValue {
    public class AirshipArray {
        public AirshipSerializedProperty property { get; }
        public PropertyType elementType =>
            GetTypeFromTypeString(property.serializedItems.FindPropertyRelative("type").stringValue);

        public string elementObjectTypeString => property.serializedItems.FindPropertyRelative("objectType").stringValue;
        
        [CanBeNull]
        public Type elementObjectType => TypeReflection.GetTypeFromString(property.serializedItems.FindPropertyRelative("objectType").stringValue);
        [CanBeNull]
        public AirshipType elementAirshipType => AirshipBuildInfo.Instance.GetTypeByName(property.serializedItems.FindPropertyRelative("objectType").stringValue);
        
        private readonly SerializedProperty serializedItems;
        private readonly SerializedProperty serializedObjects;

        public bool prefabOverride => serializedItems.prefabOverride || serializedObjects.prefabOverride;

        internal void RevertPropertyOverride(InteractionMode interactionMode) {
            if (!this.prefabOverride) return;
            PrefabUtility.RevertPropertyOverride(this.serializedItems, interactionMode);
            PrefabUtility.RevertPropertyOverride(this.serializedObjects, interactionMode);
        }

        internal void ApplyPropertyOverride(string assetPath, InteractionMode interactionMode) {
            if (!this.prefabOverride) return;
            PrefabUtility.ApplyPropertyOverride(this.serializedItems, assetPath, interactionMode);
            PrefabUtility.ApplyPropertyOverride(this.serializedObjects, assetPath, interactionMode);
        }

        public AirshipArray(AirshipSerializedProperty parentProperty, SerializedProperty serializedItems,
            SerializedProperty objectRefs) {
            this.property = parentProperty;
            this.serializedItems = serializedItems;
            this.serializedObjects = objectRefs;
        }

        public int size {
            get {
                return this.serializedItems.arraySize;
            }
        }

        public AirshipArrayItem PushElement() {
            int index = this.serializedItems.arraySize;
            this.serializedItems.InsertArrayElementAtIndex(index);
            this.serializedObjects.InsertArrayElementAtIndex(index);
            return GetElementAtIndex(index);
        }
        
        public AirshipArrayItem PushElement(Object obj) {
            if (this.elementType == PropertyType.Object) {
                var element = PushElement();
                element.objectReferenceValue = obj;
                return element;
            } else if (this.elementType == PropertyType.AirshipBehaviour && obj is AirshipComponent component) {
                var element = PushElement();
                element.objectReferenceValue = component;
                return element;
            }

            return null;
        }
        
        public AirshipArrayItem GetElementAtIndex(int index) {
            var value = this.serializedItems.GetArrayElementAtIndex(index);
            var obj = this.serializedObjects.GetArrayElementAtIndex(index);
            return new AirshipArrayItem(property, index, value, obj);
        }

        public void PopElement() {
            var last = this.serializedItems.arraySize - 1;
            this.serializedItems.DeleteArrayElementAtIndex(last);
            this.serializedObjects.DeleteArrayElementAtIndex(last);
        }

        public void RemoveElementAtIndex(int index) {
            this.serializedItems.DeleteArrayElementAtIndex(index);
            this.serializedObjects.DeleteArrayElementAtIndex(index);
        }

        public void MoveArrayElement(int srcIndex, int dstIndex) {
            this.serializedItems.MoveArrayElement(srcIndex, dstIndex);
            this.serializedObjects.MoveArrayElement(srcIndex, dstIndex);
        }

        public void Resize(int newSize) {
            this.serializedItems.arraySize = newSize;
            this.serializedObjects.arraySize = newSize;
        }

        public static implicit operator SerializedProperty(AirshipArray array) {
            if (array.elementType is PropertyType.Object or PropertyType.AirshipBehaviour) {
                return array.serializedObjects;
            } else {
                return array.serializedItems;
            }
        }
    }
    
    public class AirshipArrayItem : AirshipSerializedValue {
        public AirshipArrayItem(AirshipSerializedProperty parentSerializedProperty, int index, SerializedProperty valueProperty, SerializedProperty objectValueProperty) {
            serializedModified = parentSerializedProperty.serializedModified;

            serializedType = parentSerializedProperty.serializedItems.FindPropertyRelative("type");
            serializedObjectType = parentSerializedProperty.serializedItems.FindPropertyRelative("objectType");
        
            serializedObjectValue = objectValueProperty;
            serializedValue = valueProperty;

            serializedFileRef = parentSerializedProperty.serializedFileRef;
            serializedRef = parentSerializedProperty.serializedRef;

            propertyMetadata = parentSerializedProperty.propertyMetadata;
            decorators = parentSerializedProperty.propertyMetadata.GetDecorators();

            this.editor = parentSerializedProperty.editor;
        }
    }
    
    internal SerializedProperty serializedProperty;
    internal SerializedProperty serializedItems { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }

    public bool isArray => serializedType.stringValue == "Array";

    /// <summary>
    /// Whether or not this property was modified in a prefab
    /// </summary>
    public virtual bool prefabOverride {
        get => this.serializedValue.prefabOverride || this.serializedObjectValue.prefabOverride || (isArray && array.prefabOverride);
    }
    
    public int arraySize {
        get {
            return serializedItems.FindPropertyRelative("serializedItems").arraySize;
        }
    }

    public AirshipArray array {
        get {
            UpdateProperty();
            return new AirshipArray(
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
        // var defaultValue = propertyMetadata.defaultValue;

        propertyMetadata.SetDefaultAsValue();
        propertyMetadata.modified = false;

        // if (isArray) {
        //     return;
        // }
        //
        // if (isObject) {
        //     serializedObjectValue.objectReferenceValue = null;
        //     return;
        // }
        //
        // if (defaultValue == null) {
        //     switch (type) {
        //         case PropertyType.Number:
        //             numberValue = 0;
        //             isModified = false;
        //             break;
        //         case PropertyType.String:
        //             stringValue = "";
        //             isModified = false;
        //             break;
        //         case PropertyType.Boolean:
        //             boolValue = false;
        //             isModified = false;
        //             break;
        //         case PropertyType.Enum when enumType != null:
        //             enumValue = enumType.members[0];
        //             isModified = false;
        //             break;
        //         case PropertyType.LayerMask:
        //         case PropertyType.FlagEnum:
        //             intValue = 0;
        //             break;
        //     }
        //     return;
        // }
        //
        // serializedValue.stringValue =
        //     LuauMetadataPropertySerializer.SerializeAirshipProperty(defaultValue, propertyType);
    }
}