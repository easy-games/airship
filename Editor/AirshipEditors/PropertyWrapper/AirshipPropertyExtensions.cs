using System;
using System.Collections.Generic;
using Airship.Editor;
using JetBrains.Annotations;
using Luau;
using Mono.WebBrowser;
using UnityEditor;
using UnityEngine;

public static class AirshipPropertyExtensions {
    public static string GetString(this AirshipSerializedValue value) {
        if (value.type == AirshipSerializedValue.PropertyType.String) return value.stringValue;
        return null;
    }

    public static float GetNumber(this AirshipSerializedValue value) {
        if (value.type == AirshipSerializedValue.PropertyType.Number) return value.numberValue;
        return 0;
    }
    
    public static UnityEngine.Object GetObject(this AirshipSerializedValue value) {
        if (value.type is AirshipSerializedValue.PropertyType.AirshipBehaviour or AirshipSerializedValue.PropertyType.Object) return value.objectReferenceValue;
        return null;
    }

    public static T GetObject<T>(this AirshipSerializedValue value) where T : UnityEngine.Object {
        return (T) GetObject(value);
    }

    /// <summary>
    /// Grab the type of the given AirshipComponent
    /// </summary>
    /// <param name="component">The component to grab the type of</param>
    /// <returns></returns>
    public static AirshipType GetAirshipType(this AirshipComponent component) {
        if (component.script == null) return null;
        if (component.script.m_metadata == null) return null;
        return AirshipBuildInfo.Instance.GetTypeByName(component.script.m_metadata.name);
    }
    
    /// <summary>
    /// Add the given AirshipBehaviour to the GameObject
    /// </summary>
    /// <param name="gameObject">The game object</param>
    /// <param name="type">The component type to get</param>
    /// <returns></returns>
    [CanBeNull]
    public static AirshipComponent AddAirshipComponent(this GameObject gameObject, AirshipType type) {
        if (type == null) {
            throw new InvalidCastException("Cannot add invalid type");
        }
        
        if (!type.AirshipBehaviour) return null;
        
        var airshipScript = type.Script;
        if (airshipScript == null) throw new InvalidCastException($"Found type without script - {type.UniqueId}");

        var component = gameObject.AddComponent<AirshipComponent>();
        component.script = airshipScript;
        component.scriptPath = airshipScript.m_path;
        EditorUtility.SetDirty(component);
        return component;
    }
    
    /// <summary>
    /// Gets an AirshipComponent of the given type, if it exists on the GameObject
    /// </summary>
    /// <param name="type">The airship component type to get</param>
    /// <returns></returns>
    [CanBeNull]
    public static AirshipComponent GetAirshipComponent(this GameObject gameObject, AirshipType type) {
        if (type == null) return null;
        if (!type.AirshipBehaviour) return null;
        
        foreach (var airshipComponent in gameObject.GetComponents<AirshipComponent>()) {
            var componentType = airshipComponent.GetAirshipType();
            if (componentType == null) continue; 
            
            if (airshipComponent.GetAirshipType() == type) {
                return airshipComponent;
            }
        }

        return null;
    }
    
    /// <summary>
    /// Gets the airship components of the given type on the GameObject
    /// </summary>
    /// <param name="type">The component type</param>
    /// <returns></returns>
    public static AirshipComponent[] GetAirshipComponents(this GameObject gameObject, AirshipType type) {
        List<AirshipComponent> components = new List<AirshipComponent>();
        
        if (type == null) return null;
        foreach (var airshipComponent in gameObject.GetComponents<AirshipComponent>()) {
            var componentType = airshipComponent.GetAirshipType();
            if (componentType == null) continue; 
            
            if (airshipComponent.GetAirshipType() == type) {
                components.Add(airshipComponent);
            }
        }

        return components.ToArray();
    }
}
