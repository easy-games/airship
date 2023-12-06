using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class TypeReflection {
    private static readonly List<string> _namespaces = new();
    private static readonly Dictionary<string, Type> _shortTypeNames = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reload() {
        _namespaces.Clear();
        _shortTypeNames.Clear();
        SetupNamespaceStrings();
    }
    
    private static void SetupNamespaceStrings() {
        _namespaces.Add("UnityEngine");
        _namespaces.Add("UnityEngine.PhysicsModule");
        _namespaces.Add("UnityEngine.CoreModule");
        _namespaces.Add("UnityEngine.AudioModule");
        _namespaces.Add("FishNet.Object");
        _namespaces.Add("UnityEngine.UI");
        _namespaces.Add("TMPro");
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
        if (_namespaces.Count == 0) {
            SetupNamespaceStrings();
        }
        var res = _shortTypeNames.TryGetValue(name, out var result);
        if (res) {
            return result;
        }

        var simple = Type.GetType(name);
        if (simple != null) {
            return simple;
        }

        foreach (var str in _namespaces) {
            var concat = name + ", " + str;
            var returnType = Type.GetType(concat);
            if (returnType != null) {
                _shortTypeNames.Add(name, returnType);
                return returnType;
            }
        }

        foreach (var ns in _namespaces) {
            var concat = "UnityEngine." + name + ", " + ns;
            if (ns == "TMPro") {
                concat = $"{ns}.{name}, Unity.TextMeshPro";
            }
            var returnType = Type.GetType(concat);
            if (returnType != null) {
                _shortTypeNames.Add(name, returnType);
                return returnType;
            }
        }

        return null;
    }
}
