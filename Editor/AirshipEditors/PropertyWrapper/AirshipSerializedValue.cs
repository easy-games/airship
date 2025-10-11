using System;
using System.Collections.Generic;
using System.Globalization;
using JetBrains.Annotations;
using Luau;
using UnityEditor;

public abstract class AirshipSerializedValue {
    public enum PropertyType {
        Unknown,
        String,
        Number,
        Boolean,
        Object,
        AirshipBehaviour,
        LayerMask,
        AnimationCurve,
        Enum,
        FlagEnum,
        Array,
    }

    public static PropertyType GetTypeFromTypeString(string type) {
        return type switch {
            "string" => PropertyType.String,
            "number" => PropertyType.Number,
            "boolean" => PropertyType.Boolean,
            "object" => PropertyType.Object,
            "AirshipBehaviour" => PropertyType.AirshipBehaviour,
            "AnimationCurve" => PropertyType.AnimationCurve,
            "LayerMask" => PropertyType.LayerMask,
            "FlagEnum" => PropertyType.FlagEnum,
            "IntEnum" or "StringEnum" => PropertyType.Enum,
            "Array" => PropertyType.Array,
            _ => PropertyType.Unknown,
        };
    }
    
    public const string StringType = "string";
    public const string NumberType = "number";
    public const string BooleanType = "boolean";
    public const string LayerMaskType = "LayerMask";
    
    public const string AirshipBehaviourType = "AirshipBehaviour";
    public const string IntEnumType = "IntEnum";
    public const string StringEnumType = "StringEnum";
    public const string FlagEnumType = "FlagEnum";
    public const string ObjectType = "object";
    
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
    public bool isModified {
        get {
            return serializedModified.boolValue;
        }
        internal set {
            serializedModified.boolValue = value;
        }
    }
    
    public bool isAirshipType => serializedType.stringValue == "AirshipBehaviour";
    public bool isEnum => serializedType.stringValue is "IntEnum" or "StringEnum" or "FlagEnum";
    public bool isObject => serializedType.stringValue == "object";
    public PropertyType type => GetTypeFromTypeString(serializedType.stringValue);
    public string typeString => serializedType.stringValue;
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
            return serializedValue.stringValue != "" && serializedValue.stringValue != "0";
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
        set {
            if (propertyType != AirshipComponentPropertyType.AirshipFloat) throw new InvalidCastException("Value is not a float");
            serializedValue.stringValue = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public int intValue {
        get {
            if (propertyType != AirshipComponentPropertyType.AirshipLayerMask 
                && propertyType != AirshipComponentPropertyType.AirshipInt) throw new InvalidCastException("Value is not an integer");
            int.TryParse(serializedValue.stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var currentValue);
            return currentValue;
        }
        set {
            if (propertyType != AirshipComponentPropertyType.AirshipLayerMask 
                && propertyType != AirshipComponentPropertyType.AirshipInt) throw new InvalidCastException("Value is not an integer");
            serializedValue.stringValue = value.ToString(CultureInfo.InvariantCulture);
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
                if (int.TryParse(serializedValue.stringValue, out var intValue)) {
                    return enumType.members.Find(f => f.IntValue == intValue);
                }

                return enumType.members[0];
            } else {
                var strValue = serializedValue.stringValue;
                return enumType.members.Find(f => f.StringValue == strValue) ?? enumType.members[0];
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

    public bool HasDecorator(string targetDecoratorName) {
        if (decorators == null) {
            return false;
        }
        
        foreach (var decorator in decorators) {
            if (decorator.name == targetDecoratorName) return true;
        }

        return false;
    }
}