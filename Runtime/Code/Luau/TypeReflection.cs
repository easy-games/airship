using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TypeReflection {
    // Initialized to have short term solution for most likely desired APIs by name
    private static readonly Dictionary<string, Type> _shortTypeNames = new() {
        { "Image", typeof(Image) },
        { "Transform", typeof(Transform) },
        { "RectTransform", typeof(RectTransform) },
        { "Button", typeof(Button) },
        { "Color", typeof(Color) },
        { "Toggle", typeof(Toggle) },
        // { "ToggleGroup", typeof(ToggleGroup) }
    };
    private static readonly Dictionary<string, HashSet<string>> _assemblyNamespaceCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reload() {
    }
    
    private static void RegisterBaseAPI(BaseLuaAPIClass api) {
        var name = api.GetAPIType().Name;
        _shortTypeNames[name] = api.GetAPIType();
    }

    public static Type GetTypeFromString(string name) {
        var res = _shortTypeNames.TryGetValue(name, out var result);
        if (res) {
            return result;
        }

        var typeByName = GetTypeByName(name);
        _shortTypeNames.TryAdd(name, typeByName);
        return typeByName;
    }
    
    private static Type GetTypeByName(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Check assembly cache for namespaces (or populate if missing)
            if (!_assemblyNamespaceCache.TryGetValue(assembly.FullName, out HashSet<string> allNamespaces))
            {
                allNamespaces = new HashSet<string>();
                foreach (var typ in assembly.GetTypes())
                {
                    var ns = typ.Namespace;
                    allNamespaces.Add(ns);
                }
                _assemblyNamespaceCache.Add(assembly.FullName, allNamespaces);
            }

            // Search namespace for type by name
            Type CheckNamespace(string ns) {
                var fullName = $"{ns}.{name}";
                var tt = assembly.GetType(fullName);
                if (tt != null) {
                    _shortTypeNames.TryAdd(name, tt);
                    return tt;
                }
                return null;
            }
            
            // Check UnityEngine namespace first
            var unityEngineType = CheckNamespace("UnityEngine");
            if (unityEngineType != null) return unityEngineType;

            // Check other namespaces in assembly
            foreach (var ns in allNamespaces) {
                var namespaceType = CheckNamespace(ns);
                if (namespaceType != null) return namespaceType;
            }
        }

        return null;
    }
}
