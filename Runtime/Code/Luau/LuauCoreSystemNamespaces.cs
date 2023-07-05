using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public partial class LuauCore
{
    //List of all the namespace strings to include for "AddComponent( singleName )" checks
    private void SetupNamespaceStrings()
    {
        namespaces.Add("UnityEngine");
        namespaces.Add("UnityEngine.PhysicsModule");
        namespaces.Add("UnityEngine.CoreModule");
        namespaces.Add("UnityEngine.AudioModule");
        namespaces.Add("FishNet.Object");

        // this.RegisterComponent("NetworkObject", typeof(NetworkObject));
        // this.RegisterComponent("Canvas", typeof(Canvas));
    }

    //This is for things like GameObject:Find() etc - these all get passed to the luau dll on startup
    //Tag the class with [LuauAPI]
    //This works two ways - either derive from BaseLuaAPIClass if you're extending an existing Unity API like GameObject
    //                    - Create a brand new class and tag it, its members will be automatically reflected
    private void SetupUnityAPIClasses()
    {

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            // Loop over all types
            foreach (var type in assembly.GetTypes())
            {
                // Get custom attributes for type
                var typeAttributes = type.GetCustomAttributes(typeof(LuauAPI), true);
                if (typeAttributes.Length > 0)
                {
                    if (type.IsSubclassOf(typeof(BaseLuaAPIClass)))
                    {
                        BaseLuaAPIClass instance = (BaseLuaAPIClass)Activator.CreateInstance(type);
                        RegisterBaseAPI(instance);
                    }
                    else
                    {
                        RegisterBaseAPI(new UnityCustomAPI(type));
                    }
                }
            }
        }
    }

    private void RegisterBaseAPI(BaseLuaAPIClass api)
    {
        string name = api.GetAPIType().Name;
        unityAPIClasses[name] = api;
        unityAPIClassesByType[api.GetAPIType()] = api;

        Type[] list = api.GetDescendantTypes();

        if (list != null)
        {
            foreach (Type t in list)
            {
                unityAPIClassesByType[t] = api;
            }
        }

    }


    public void RegisterNamespace(string str)
    {
        namespaces.Add(str);
    }

    public void RegisterComponent(string componentName, Type t)
    {
        shortTypeNames.Add(componentName, t);
        Debug.Log($"Registered component \"{t.Name}\" with Namespace=\"{t.Namespace}\". You can add the namespace to LuauCoreSystemNamespaces.cs to remove this message.");
    }

    public Type GetTypeFromString(string name)
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