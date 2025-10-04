using System;
using Luau;
using UnityEditor;

public readonly struct AirshipArrayProperty {
    internal SerializedProperty serializedItemsType { get; }
    internal SerializedProperty serializedItemsObjectType { get; }
    
    public int length => 0;
    public string type => serializedItemsType.stringValue;
    public bool isAirshipType => serializedItemsType.stringValue == "AirshipBehaviour";
    public AirshipType airshipType => isAirshipType ? AirshipBuildInfo.Instance.GetTypeByName(serializedItemsObjectType.stringValue) : null;
    public Type objectType {
        get {
            if (serializedItemsType.stringValue == "object") {
                return serializedItemsObjectType.stringValue != ""
                    ? TypeReflection.GetTypeFromString(serializedItemsObjectType.stringValue)
                    : typeof(UnityEngine.Object);
            } else if (serializedItemsType.stringValue == "AirshipBehaviour") {
                return typeof(AirshipComponent);
            } else {
                return null;
            }
        }
    }
    
    public AirshipArrayProperty(AirshipProperty property) {
        serializedItemsType = property.serializedItems.FindPropertyRelative("type");
        serializedItemsObjectType = property.serializedItems.FindPropertyRelative("objectType");
    }
}