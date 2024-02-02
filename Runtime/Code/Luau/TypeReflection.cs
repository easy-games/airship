using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class TypeReflection {
    private static readonly Dictionary<string, Type> _shortTypeNames = new();
    private static readonly Dictionary<string, HashSet<string>> _assemblyNamespaceCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reload() {

    }
    private static void RegisterBaseAPI(BaseLuaAPIClass api) {
        var name = api.GetAPIType().Name;
        _shortTypeNames[name] = api.GetAPIType();
    }

    // private void SetupUnityAPIClasses() {
    //     var assemblies = AppDomain.CurrentDomain.GetAssemblies();
    //     foreach (var assembly in assemblies) {
    //         try {
    //             foreach (var type in assembly.GetTypes()) {
    //                 var typeAttributes = type.GetCustomAttributes(typeof(LuauAPI), true);
    //                 if (typeAttributes.Length == 0) continue;
    //                 if (type.IsSubclassOf(typeof(BaseLuaAPIClass))) {
    //                     var instance = (BaseLuaAPIClass)Activator.CreateInstance(type);
    //                     RegisterBaseAPI(instance);
    //                 } else {
    //                     RegisterBaseAPI(new UnityCustomAPI(type));
    //                 }
    //             }
    //         } catch (ReflectionTypeLoadException e) {
    //             foreach (var inner in e.LoaderExceptions) {
    //                 Debug.LogWarning($"Failed reflection: {inner.Message}");
    //             }
    //         }
    //     }
    // }

    public static Type GetTypeFromString(string name) {
        var res = _shortTypeNames.TryGetValue(name, out var result);
        if (res) {
            return result;
        }

        var typeByName = GetTypeByName(name);
        _shortTypeNames.Add(name, typeByName);
        return typeByName;
    }
    
    private static Type GetTypeByName(string name)
    {
        if (name == "Image") return typeof(Image);
        if (name == "Transform") return typeof(Transform);
        if (name == "RectTransform") return typeof(RectTransform);
        if (name == "Button") return typeof(Button);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse())
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

            foreach (var ns in allNamespaces)
            {
                var fullName = $"{ns}.{name}";
                var tt = assembly.GetType(fullName);
                if (tt != null)
                {
                    return tt;
                }
            }
        }

        return null;
    }
}
