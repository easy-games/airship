using System;
using System.Collections.Generic;
using Airship.Editor;
using JetBrains.Annotations;
using Luau;
using Mono.WebBrowser;
using UnityEditor;
using UnityEngine;

public static class AirshipEditorExtensions {
    public static UnityEngine.Object GetObject(this AirshipSerializedValue value) {
        if (value.type is AirshipSerializedType.AirshipBehaviour or AirshipSerializedType.Object) return value.objectReferenceValue;
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

    public static AirshipType GetAirshipType(this AirshipSerializedLuauObject luauObject) {
        if (luauObject.metadata == null) return null;
        return AirshipBuildInfo.Instance.GetTypeByName(luauObject.metadata.name);
    }

    public static LuauMetadata GetMetadataForType(this AirshipType type) {
        var script = type.Script;
        if (script == null) return null;
        if (script.m_metadata != null && script.m_metadata.name == type.Name) return script.m_metadata;
        if (script.m_serializables != null) {
            foreach (var serializable in script.m_serializables) {
                if (serializable.name == type.Name) {
                    return serializable;
                }
            }
        }

        return null;
    }

    public static AirshipType GetAirshipType(this AirshipScriptableObject scriptableObject) {
        if (scriptableObject.script == null) return null;
        if (scriptableObject.script.m_metadata == null) return null;
        return AirshipBuildInfo.Instance.GetTypeByName(scriptableObject.script.m_metadata.name);
    }

    private static IEnumerable<string> GetPathParts(Transform transform) {
        var parent = transform;
        while (parent != null) {
            yield return parent.name;
            parent = parent.transform.parent;
        }
    }

    internal static string GetFullName(this GameObject gameObject) {
        var parts = new List<string>();
        parts.AddRange(GetPathParts(gameObject.transform));
        parts.Reverse();
        return "/" + string.Join("/", parts);
    }

    public static string NicifyName(this AirshipType type) {
        return ObjectNames.NicifyVariableName(type.Name);
    }
    
    /// <summary>
    /// Grab the type of component of the given airship script, or null if no component attached to the script
    /// </summary>
    /// <param name="script"></param>
    /// <returns></returns>
    [CanBeNull]
    public static AirshipType GetComponentType(this AirshipScript script) {
        // TODO: Get types by path
        if (script.m_metadata != null) {
            return AirshipBuildInfo.Instance.GetTypeByPathAndName(script.assetPath, script.m_metadata.name) ?? AirshipBuildInfo.Instance.GetTypeByName(script.m_metadata.name);
        }
        
        return null;
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
        
        if (type.DeclarationType != AirshipDeclarationType.AirshipBehaviour) return null;
        
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
        if (type.DeclarationType != AirshipDeclarationType.AirshipBehaviour) return null;
        
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
        var components = new List<AirshipComponent>();
        
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
    
    public static AirshipComponent GetAirshipComponentInChildren(this GameObject gameObject, AirshipType type) {
        if (type == null) return null;
        foreach (var airshipComponent in gameObject.GetComponentsInChildren<AirshipComponent>()) {
            var componentType = airshipComponent.GetAirshipType();
            if (componentType == null) continue; 
            
            if (airshipComponent.GetAirshipType() == type) {
                return airshipComponent;
            }
        }

        return null;
    }
    
    public static AirshipComponent[] GetAirshipComponentsInChildren(this GameObject gameObject, AirshipType type) {
        var components = new List<AirshipComponent>();
        
        if (type == null) return null;
        foreach (var airshipComponent in gameObject.GetComponentsInChildren<AirshipComponent>()) {
            var componentType = airshipComponent.GetAirshipType();
            if (componentType == null) continue; 
            
            if (airshipComponent.GetAirshipType() == type) {
                components.Add(airshipComponent);
            }
        }

        return components.ToArray();
    }
}
