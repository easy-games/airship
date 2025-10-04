using System;
using System.Collections.Generic;
using System.Globalization;
using Luau;
using UnityEditor;
using UnityEngine;

public struct AirshipProperty {
    public class DecoratorParameter {
        internal SerializedProperty serializedValue;
        internal SerializedProperty serializedType;

        public DecoratorParameter(SerializedProperty parameter) {
            serializedValue = parameter.FindPropertyRelative("serializedValue");
            serializedType = parameter.FindPropertyRelative("type");
        }

        public string stringValue => serializedValue.stringValue;
        public int intValue => int.Parse(serializedValue.stringValue);
        public float floatValue => float.Parse(serializedValue.stringValue);
        public bool boolValue => bool.Parse(serializedValue.stringValue);
    }
    
    public class Decorator {
        internal SerializedProperty serializedName;
        internal SerializedProperty serializedParameters;

        public string name => serializedName.stringValue;
        public DecoratorParameter[] parameters {
            get {
                var parameters = new DecoratorParameter[serializedParameters.arraySize];
                
                for (var i = 0; i < serializedParameters.arraySize; i++) {
                    parameters[i] = new DecoratorParameter(serializedParameters.GetArrayElementAtIndex(i));
                }

                return parameters;
            }
        }

        public Decorator(SerializedProperty decorator) {
            serializedName = decorator.FindPropertyRelative("name");
            serializedParameters = decorator.FindPropertyRelative("parameters");
        }
    }
    
    internal SerializedProperty serializedName { get; }
    internal SerializedProperty serializedType { get; }
    internal SerializedProperty serializedObjectType { get; }
    internal SerializedProperty serializedValue { get; }
    internal SerializedProperty serializedObject { get; }
    internal SerializedProperty serializedModified { get; }
    internal SerializedProperty serializedItems { get; }
    internal SerializedProperty serializedDecorators { get; }
    internal SerializedProperty serializedRefPath { get; }
    internal SerializedProperty serializedFileRef { get; }
    internal LuauMetadataProperty propertyMetadata { get; }

    public string name => serializedName.stringValue;
    public AirshipType airshipType => isAirshipType ? AirshipBuildInfo.Instance.GetTypeByName(serializedObjectType.stringValue) : null;
    public string type => propertyMetadata.type ?? serializedType.stringValue;
    public Type objectType => serializedObjectType.stringValue != "" ? TypeReflection.GetTypeFromString(serializedObjectType.stringValue) : typeof(UnityEngine.Object);
    public bool isModified => serializedModified.boolValue;
    public bool isObject => serializedType.stringValue == "object";
    public bool isArray => serializedType.stringValue == "Array";
    public bool isEnum => serializedType.stringValue is "IntEnum" or "StringEnum" or "FlagEnum";
    public bool isAirshipType => serializedType.stringValue == "AirshipBehaviour";

    public string enumRef => this.serializedRefPath.stringValue;
    public TypeScriptEnum @enum => AirshipEditorInfo.Enums.GetEnum(this.serializedRefPath.stringValue);
    public TyperScriptEnumMember selectedEnumMember {
        get {
            if (@enum == null) return null;
            if (@enum.memberType == TypeScriptEnumMemberType.Integer) {
                var intValue = int.Parse(serializedValue.stringValue);
                return @enum.members.Find(f => f.IntValue == intValue);
            } else {
                var strValue = serializedValue.stringValue;
                return @enum.members.Find(f => f.StringValue == strValue);
            }
        }
        set {
            if (@enum == null) throw new InvalidCastException("Invalid cast");
            this.serializedValue.stringValue = @enum.memberType == TypeScriptEnumMemberType.Integer ? value.IntValue.ToString(CultureInfo.InvariantCulture) : value.StringValue;
        }
    }
    
    public List<LuauMetadataDecoratorElement> decorators { get; private set; }

    public bool TryGetDecorator(string targetDecoratorName, out List<LuauMetadataDecoratorValue> parameters) {
        foreach (var decorator in decorators) {
            if (decorator.name == targetDecoratorName) {
                parameters = decorator.parameters;
                return true;
            }
        }

        parameters = default;
        return false;
    }
    
    public AirshipArrayProperty arrayValue {
        get {
            return new AirshipArrayProperty(this);
        }
    }
    
    public UnityEngine.Object objectReferenceValue {
        get {
            return serializedObject.objectReferenceValue;
        }
        set {
            if (value != serializedObject.objectReferenceValue) serializedModified.boolValue = true;
            serializedObject.objectReferenceValue = value;
        }
    }

    public string stringValue {
        get {
            return serializedValue.stringValue;
        }
        set {
            if (type != "string") throw new InvalidCastException("Expected string");
            serializedValue.stringValue = value;
            serializedModified.boolValue = true;
        }
    }

    public bool boolValue {
        get {
            return serializedValue.stringValue == "1";
        }
        set {
            if (type != "boolean") throw new InvalidCastException("Expected string");
            serializedValue.stringValue = value ? "1" : "0";
        }
    }

    public float floatValue {
        get {
            float.TryParse(serializedValue.stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentValue);
            return currentValue;
        }
        set => serializedValue.stringValue = value.ToString(CultureInfo.InvariantCulture);
    }

    public void ResetToDefaultValue() {
        // TODO: Reset to default value (if applicable)
    }

    public AirshipProperty(SerializedProperty property, LuauMetadataProperty metadata) {
        serializedName = property.FindPropertyRelative("name");
        serializedType = property.FindPropertyRelative("type");
        serializedValue = property.FindPropertyRelative("serializedValue");
        serializedObject = property.FindPropertyRelative("serializedObject");
        serializedObjectType = property.FindPropertyRelative("objectType");
        serializedModified = property.FindPropertyRelative("modified");
        serializedItems = property.FindPropertyRelative("serializedItems");
        serializedDecorators = property.FindPropertyRelative("decorators");
        serializedRefPath = property.FindPropertyRelative("refPath");
        serializedFileRef = property.FindPropertyRelative("fileRef");
        propertyMetadata = metadata;
        
        // Debug.Log($"Create property {serializedName.stringValue}, with decorators {serializedDecorators.arraySize}, {metadata.GetDecorators().Count}");
        // decorators = new Decorator[0];

        decorators = metadata.GetDecorators();
    }

    public static bool operator ==(AirshipProperty lhs, AirshipProperty rhs) {
        return lhs.serializedName == rhs.serializedName;
    }
    
    public static bool operator !=(AirshipProperty lhs, AirshipProperty rhs) {
        return lhs.serializedName != rhs.serializedName;
    }
    
    public static implicit operator bool(AirshipProperty property) {
        return property.serializedName != default;
    }
}