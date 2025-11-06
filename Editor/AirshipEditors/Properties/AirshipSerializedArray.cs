using System;
using JetBrains.Annotations;
using Luau;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

public class AirshipSerializedArray {
    public AirshipSerializedProperty property { get; }
    public AirshipSerializedType elementType =>
        AirshipSerializedValue.GetTypeFromTypeString(property.serializedItems.FindPropertyRelative("type").stringValue);

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

    internal void ResetToDefault() {
        var propertyMetadata = property.propertyMetadata;
        var defaultArray = propertyMetadata.defaultValue as JArray;
        if (defaultArray != null) {
            string[] serializedElements = new string[defaultArray.Count];
            var propertyType =
                LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(propertyMetadata.items
                    .type, false);
            
            for (var i = 0; i < defaultArray.Count; i++) {
                var obj = defaultArray[i].Value<object>();
                serializedElements[i] =
                    LuauMetadataPropertySerializer.SerializeAirshipProperty(obj, propertyType);
            }

            serializedItems.ClearArray();
            serializedObjects.ClearArray();

            serializedItems.arraySize = serializedElements.Length;
            for (var i = 0; i < serializedElements.Length; i++) {
                var serializedElement = serializedElements[i];
                serializedItems.GetArrayElementAtIndex(i).stringValue = serializedElement;
            }
        } else {
            serializedItems.ClearArray();
        }
    }
    
    public AirshipSerializedArray(AirshipSerializedProperty parentProperty, SerializedProperty serializedItems,
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
    
    public AirshipSerializedArrayValue PushElement() {
        int index = this.serializedItems.arraySize;
        this.serializedItems.InsertArrayElementAtIndex(index);
        this.serializedObjects.InsertArrayElementAtIndex(index);
        return GetElementAtIndex(index);
    }
    
    public AirshipSerializedArrayValue InsertLastElement(Object obj) {
        if (this.elementType == AirshipSerializedType.Object) {
            var element = PushElement();
            element.objectReferenceValue = obj;
            return element;
        } else if (this.elementType == AirshipSerializedType.AirshipBehaviour && obj is AirshipComponent component) {
            var element = PushElement();
            element.objectReferenceValue = component;
            return element;
        }

        return null;
    }
    
    public AirshipSerializedArrayValue GetElementAtIndex(int index) {
        var value = this.serializedItems.GetArrayElementAtIndex(index);
        var obj = this.serializedObjects.GetArrayElementAtIndex(index);
        return new AirshipSerializedArrayValue(property, index, value, obj);
    }


    public AirshipSerializedArrayValue this[int index] {
        get => GetElementAtIndex(index);
    }

    public void RemoveElementAtEnd() {
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

    public void ClearArray() {
        this.serializedItems.ClearArray();
        this.serializedObjects.ClearArray();
    }

    public void ResizeArray(int newSize) {
        this.serializedItems.arraySize = newSize;
        this.serializedObjects.arraySize = newSize;
    }

    public static implicit operator SerializedProperty(AirshipSerializedArray array) {
        if (array.elementType is AirshipSerializedType.Object or AirshipSerializedType.AirshipBehaviour) {
            return array.serializedObjects;
        } else {
            return array.serializedItems;
        }
    }
}