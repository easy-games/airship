using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class TypeReflection {
    private static readonly Lazy<TypeReflection> _instance = new(() => new TypeReflection());
    public static TypeReflection Instance => _instance.Value;

    private readonly List<string> _namespaces = new();
    private readonly Dictionary<string, Type> _shortTypeNames = new();

    private TypeReflection() {
        SetupNamespaceStrings();
        // SetupUnityAPIClasses();
    }
    
    private void SetupNamespaceStrings() {
        _namespaces.Add("UnityEngine");
        _namespaces.Add("UnityEngine.PhysicsModule");
        _namespaces.Add("UnityEngine.CoreModule");
        _namespaces.Add("UnityEngine.AudioModule");
        _namespaces.Add("FishNet.Object");
        _namespaces.Add("UnityEngine.UI");
    }

    private void RegisterBaseAPI(BaseLuaAPIClass api) {
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

    public Type GetTypeFromString(string name) {
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

        foreach (var str in _namespaces) {
            var concat = "UnityEngine." + name + ", " + str;
            var returnType = Type.GetType(concat);
            if (returnType != null) {
                _shortTypeNames.Add(name, returnType);
                return returnType;
            }
        }

        return null;
    }
}
