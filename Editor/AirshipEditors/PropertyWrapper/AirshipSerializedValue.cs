using System;
using System.Collections.Generic;
using System.Globalization;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEngine;

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
        Color,
        Quaternion,
        Vector2,
        Vector3,
        Vector4,
        Matrix4x4,
        Rect
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
            "Color" => PropertyType.Color,
            "Quaternion" => PropertyType.Quaternion,
            "Vector2" => PropertyType.Vector2,
            "Vector3" => PropertyType.Vector3,
            "Vector4" => PropertyType.Vector4,
            "Matrix4x4" => PropertyType.Matrix4x4,
            "Rect" => PropertyType.Rect,
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

    public Color colorValue {
        get {
            if (type != PropertyType.Color) throw new InvalidCastException("Value is not a Color");
            return serializedValue.stringValue != "" ? JsonUtility.FromJson<Color>(serializedValue.stringValue) : default;
        }
        set {
            if (type != PropertyType.Color) throw new InvalidCastException("Value is not a Color");
            serializedValue.stringValue = JsonUtility.ToJson(value);
        }
    }

    public Vector2 vector2Value {
        get {
            if (type != PropertyType.Vector2) throw new InvalidCastException("Value is not a Vector2");
            return serializedValue.stringValue != "" ? JsonUtility.FromJson<Vector2>(serializedValue.stringValue) : default;
        }
        set {
            if (type != PropertyType.Vector2) throw new InvalidCastException("Value is not a Vector2");
            serializedValue.stringValue = JsonUtility.ToJson(value);
        }
    }
    
    public Vector3 vector3Value {
        get {
            if (type != PropertyType.Vector3) throw new InvalidCastException("Value is not a Vector3");
            return serializedValue.stringValue != "" ? JsonUtility.FromJson<Vector3>(serializedValue.stringValue) : default;
        }
        set {
            if (type != PropertyType.Vector3) throw new InvalidCastException("Value is not a Vector3");
            serializedValue.stringValue = JsonUtility.ToJson(value);
        }
    }
    
    public Vector4 vector4Value {
        get {
            if (type != PropertyType.Vector4) throw new InvalidCastException("Value is not a Vector4");
            return serializedValue.stringValue != "" ? JsonUtility.FromJson<Vector4>(serializedValue.stringValue) : default;
        }
        set {
            if (type != PropertyType.Vector4) throw new InvalidCastException("Value is not a Vector4");
            serializedValue.stringValue = JsonUtility.ToJson(value);
        }
    }

    public Rect rectValue {
        get {
            if (type != PropertyType.Rect) throw new InvalidCastException("Value is not a Rect");
            return LuauMetadataPropertySerializer.DeserializeRect(serializedValue.stringValue);
        }
        set {
            if (type != PropertyType.Rect) throw new InvalidCastException("Value is not a Rect");
            serializedValue.stringValue = LuauMetadataPropertySerializer.SerializeRect(value);
        }
    }

    public Quaternion quaternionValue {
        get {
            if (type != PropertyType.Quaternion) throw new InvalidCastException("Value is not a Quaternion");
            var value = serializedValue.stringValue;
            return value == "" ? default : JsonUtility.FromJson<Quaternion>(value);
        }
        set {
            if (type != PropertyType.Quaternion) throw new InvalidCastException("Value is not a Quaternion");
            serializedValue.stringValue = JsonUtility.ToJson(value);
        }
    }
    
    public Matrix4x4 matrix4x4Value {
        get {
            if (type != PropertyType.Matrix4x4) throw new InvalidCastException("Value is not a Matrix4x4");
            var value = serializedValue.stringValue;
            return value == "" ? default : JsonUtility.FromJson<Matrix4x4>(value);
        }
        set {
            if (type != PropertyType.Matrix4x4) throw new InvalidCastException("Value is not a Matrix4x4");
            serializedValue.stringValue = JsonUtility.ToJson(value);
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