using System;
using System.Collections.Generic;
using UnityEngine;

public partial class LuauCore
{
    private static List<string> namespaces = new();
    private static Dictionary<string, Type> shortTypeNames = new();
    
    static LuauCore()
    {
#if UNITY_EDITOR
        SetupNamespaceStrings();
#endif
    }
    
    //List of all the namespace strings to include for "AddComponent( singleName )" checks
    private static void SetupNamespaceStrings()
    {
        namespaces.Add("UnityEngine");
        namespaces.Add("UnityEngine.PhysicsModule");
        namespaces.Add("UnityEngine.CoreModule");
        namespaces.Add("UnityEngine.AudioModule");
        namespaces.Add("FishNet.Object");
        namespaces.Add("UnityEngine.UI");

        // this.RegisterComponent("NetworkObject", typeof(NetworkObject));
        // this.RegisterComponent("Canvas", typeof(Canvas));
    }

    public void RegisterBaseAPI(BaseLuaAPIClass api)
    {
        string name = api.GetAPIType().Name;
        if (!shortTypeNames.TryAdd(name, api.GetAPIType())) {
            return;
        }
        unityAPIClasses[name] = api;
        unityAPIClassesByType[api.GetAPIType()] = api;
        // shortTypeNames.Add(name, api.GetAPIType());

        Type[] list = api.GetDescendantTypes();

        if (list != null)
        {
            foreach (Type t in list)
            {
                unityAPIClassesByType[t] = api;
                unityAPIClasses.TryAdd(t.Name, api);
            }
        }
    }


    public void RegisterNamespace(string str)
    {
        namespaces.Add(str);
    }

    /// <summary>
    /// Registers a component to be used in things like AddComponent()
    /// </summary>
    /// <param name="t"></param>
    public void RegisterComponent(Type t) {
        shortTypeNames.TryAdd(t.Name, t);
        // Debug.Log($"Registered component \"{t.Name}\" with Namespace=\"{t.Namespace}\". You can add the namespace to LuauCoreSystemNamespaces.cs to remove this message.");
    }

    public static Type GetTypeFromString(string name)
    {

        bool res = shortTypeNames.TryGetValue(name, out Type result);
        if (res)
        {
            return result;
        }
        //Try and get it as-is
        Type simple = Type.GetType(name);
        if (simple != null)
        {
            return simple;
        }
        
        //Check a bunch of namespaces part 1
        foreach (string str in namespaces)
        {
            string concat = name + ", " + str;
            Type returnType = Type.GetType(concat);

            if (returnType != null)
            {
                shortTypeNames.Add(name, returnType);
                return returnType;
            }
        }

        //Check a bunch of namespaces part 2
        foreach (string str in namespaces)
        {
            string concat = "UnityEngine." + name + ", " + str;
            Type returnType = Type.GetType(concat);

            if (returnType != null)
            {
                shortTypeNames.Add(name, returnType);
                return returnType;
            }
        }

        return null;
    }
}