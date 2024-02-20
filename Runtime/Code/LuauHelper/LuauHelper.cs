using System;
using System.Reflection;
using DavidFDev.DevConsole;
using UnityEngine;

public class LuauHelper : Singleton<LuauHelper> {
    private void OnEnable() {
        LuauCore.onSetupReflection += this.LuauCore_OnSetupReflection;
        DevConsole.EnableConsole();
    }

    private void OnDisable() {
        LuauCore.onSetupReflection -= this.LuauCore_OnSetupReflection;
    }

    private void LuauCore_OnSetupReflection() {
        LuauCore.AddExtensionMethodsFromNamespace(typeof(GameObject), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "Assembly-CSharp", "");
        LuauCore.AddTypeExtensionMethodsFromClass(typeof(Component), typeof(UnityTweenExtensions));
        LuauCore.AddTypeExtensionMethodsFromClass(typeof(GameObject), typeof(UnityTweenExtensions));

        this.SetupUnityAPIClasses();
    }

    //This is for things like GameObject:Find() etc - these all get passed to the luau dll on startup
    //Tag the class with [LuauAPI]
    //This works two ways - either derive from BaseLuaAPIClass if you're extending an existing Unity API like GameObject
    //                    - Create a brand new class and tag it, its members will be automatically reflected
    private void SetupUnityAPIClasses() {

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies) {
            // Loop over all types
            try {
                foreach (var type in assembly.GetTypes()) {
                    // Get custom attributes for type
                    var typeAttributes = type.GetCustomAttributes(typeof(LuauAPI), true);
                    if (typeAttributes.Length > 0) {
                        if (type.IsSubclassOf(typeof(BaseLuaAPIClass))) {
                            BaseLuaAPIClass instance = (BaseLuaAPIClass)Activator.CreateInstance(type);
                            LuauCore.CoreInstance.RegisterBaseAPI(instance);
                        } else {
                            LuauCore.CoreInstance.RegisterBaseAPI(new UnityCustomAPI(type));
                        }
                    }
                }
            } catch (ReflectionTypeLoadException ex) {
                // now look at ex.LoaderExceptions - this is an Exception[], so:
                foreach (Exception inner in ex.LoaderExceptions) {
                    // write details of "inner", in particular inner.Message
                    Debug.LogWarning("Failed reflection: " + inner.Message);
                }
            }
        }
    }
}
