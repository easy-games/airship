using System;
using System.Collections.Generic;
using System.Globalization;
using JetBrains.Annotations;
using Luau;
using UnityEditor;

public abstract class AirshipSerializedValue {
    internal SerializedProperty serializedName { get; set; }
    internal SerializedProperty serializedModified { get; set; }
    internal SerializedProperty serializedType { get; set; }
    internal SerializedProperty serializedValue { get; set; }
    internal SerializedProperty serializedFileRef { get; set; }
    internal SerializedProperty serializedRef { get; set; }
    internal SerializedProperty serializedObjectType { get; set; }
    internal SerializedProperty serializedObjectValue { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }
    public string name => serializedName.stringValue;
    public bool isModified => serializedModified.boolValue;
    public bool isAirshipType => serializedType.stringValue == "AirshipBehaviour";
    public bool isEnum => serializedType.stringValue is "IntEnum" or "StringEnum" or "FlagEnum";
    public bool isObject => serializedType.stringValue == "object";
    
    public string type => serializedType.stringValue;
    [CanBeNull]
    public Type objectType => isObject ? TypeReflection.GetTypeFromString(serializedObjectType.stringValue) : null;
    
    [CanBeNull]
    public AirshipType airshipType => isAirshipType ? AirshipBuildInfo.Instance.GetTypeByName(serializedObjectType.stringValue) : null;
    
    [CanBeNull]
    public TypeScriptEnum enumType => isEnum ? AirshipEditorInfo.Enums.GetEnum(this.serializedRef.stringValue) : null;

    public AirshipComponentPropertyType propertyType =>
        LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(serializedType.stringValue, false);

    public UnityEngine.Object objectReferenceValue {
        get => isObject || isAirshipType ? serializedObjectValue.objectReferenceValue : null;
        set {
            if (!isObject && !isAirshipType) {
                throw new InvalidCastException("Value is not an object");
            }

            serializedObjectValue.objectReferenceValue = value;
        }
    }

    public bool boolValue {
        get {
            if (propertyType != AirshipComponentPropertyType.AirshipBoolean) throw new InvalidCastException("Value is not a boolean");
            return serializedValue.stringValue != "0";
        }
        set {
            if (propertyType != AirshipComponentPropertyType.AirshipBoolean) throw new InvalidCastException("Value is not a boolean");
            serializedValue.stringValue = value ? "1" : "0";
        }
    }

    public float numberValue {
        get {
            if (propertyType != AirshipComponentPropertyType.AirshipFloat) throw new InvalidCastException("Value is not a float");
            float.TryParse(serializedValue.stringValue, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var currentValue);
            return currentValue;
        }
    }

    public string stringValue {
        get {
            if (propertyType != AirshipComponentPropertyType.AirshipString) throw new InvalidCastException("Value is not a string");
            return serializedValue.stringValue;
        }
        set {
            if (propertyType != AirshipComponentPropertyType.AirshipString) throw new InvalidCastException("Value is not a string");
            serializedValue.stringValue = value;
        }
    }

    public TyperScriptEnumMember enumValue {
        get {
            if (enumType == null) return null;
            if (enumType.memberType == TypeScriptEnumMemberType.Integer) {
                var intValue = int.Parse(serializedValue.stringValue);
                return enumType.members.Find(f => f.IntValue == intValue);
            } else {
                var strValue = serializedValue.stringValue;
                return enumType.members.Find(f => f.StringValue == strValue);
            }
        }
        set {
            if (enumType == null) throw new InvalidCastException("Invalid cast");
            this.serializedValue.stringValue = enumType.memberType == TypeScriptEnumMemberType.Integer ? value.IntValue.ToString(CultureInfo.InvariantCulture) : value.StringValue;
        }
    }
    
    public List<LuauMetadataDecoratorElement> decorators { get; protected set; }
    public bool TryGetDecorator(string targetDecoratorName, out List<LuauMetadataDecoratorValue> parameters) {
        if (decorators == null) {
            parameters = null;
            return false;
        }
        
        foreach (var decorator in decorators) {
            if (decorator.name == targetDecoratorName) {
                parameters = decorator.parameters;
                return true;
            }
        }

        parameters = null;
        return false;
    }
}

public class AirshipSerializedProperty : AirshipSerializedValue {
    public class AirshipArray {
        public AirshipSerializedProperty property { get; }
        private readonly SerializedProperty serializedItems;
        private readonly SerializedProperty serializedObjects;
        
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

        public AirshipArrayItem GetItemAtIndex(int index) {
            var value = this.serializedItems.GetArrayElementAtIndex(index);
            var obj = this.serializedObjects.GetArrayElementAtIndex(index);
            return new AirshipArrayItem(property, index, value, obj);
        }
    }
    
    public class AirshipArrayItem : AirshipSerializedValue {
        public AirshipArrayItem(AirshipSerializedProperty parentSerializedProperty, int index, SerializedProperty valueProperty, SerializedProperty objectValueProperty) {
            serializedModified = parentSerializedProperty.serializedModified;

            serializedType = parentSerializedProperty.serializedItems.FindPropertyRelative("type");
            serializedObjectType = parentSerializedProperty.serializedItems.FindPropertyRelative("objectType");
        
            serializedObjectValue = objectValueProperty;
            serializedValue = valueProperty;

            propertyMetadata = parentSerializedProperty.propertyMetadata;
            decorators = parentSerializedProperty.propertyMetadata.GetDecorators();
        }
    }
    
    internal SerializedProperty serializedItems { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }
    internal AirshipEditor editor { get; }

    public bool isArray => serializedType.stringValue == "Array";

    public int arraySize {
        get {
            if (isArray) {
                return serializedItems.FindPropertyRelative("serializedItems").arraySize;
            } else {
                return 0;
            }
        }
    }

    public AirshipArray array {
        get {
            if (isArray) return new AirshipArray(
                this, 
                serializedItems.FindPropertyRelative("serializedItems"), 
                serializedItems.FindPropertyRelative("objectRefs"));

            return null;
        }
    }
    
    public AirshipSerializedProperty(SerializedProperty property, LuauMetadataProperty metadata, AirshipEditor editor) {
        serializedName = property.FindPropertyRelative("name");
        
        serializedType = property.FindPropertyRelative("type");
        serializedValue = property.FindPropertyRelative("serializedValue");
        serializedModified = property.FindPropertyRelative("modified");
        
        serializedObjectType = property.FindPropertyRelative("objectType");
        serializedObjectValue = property.FindPropertyRelative("serializedObject");
        
        serializedItems = property.FindPropertyRelative("items");
        serializedRef = property.FindPropertyRelative("refPath");
        serializedFileRef = property.FindPropertyRelative("fileRef");

        propertyMetadata = metadata;
        decorators = metadata.GetDecorators();
        this.editor = editor;
    }
}

