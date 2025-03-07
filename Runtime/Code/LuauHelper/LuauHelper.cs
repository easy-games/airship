using System;
using System.Reflection;
using Airship.DevConsole;
using Luau;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class LuauHelper : Singleton<LuauHelper> {

#if UNITY_EDITOR
    [MenuItem("Airship/Fix Missing UI")]
    public static void RequestMonoScriptRecompile() {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }
#endif

    private void OnEnable() {
        LuauCore.onSetupReflection += this.LuauCore_OnSetupReflection;
        DevConsole.EnableConsole();

        DevConsole.AddCommand(Command.Create("disconnect", "", "Disconnect from the server and return to Main Menu.", () => {
            TransferManager.Instance.Disconnect();
        }));
    }

    private void OnDisable() {
        LuauCore.onSetupReflection -= this.LuauCore_OnSetupReflection;
    }

    private void LuauCore_OnSetupReflection() {
        // LuauCore.AddExtensionMethodsFromNamespace(typeof(GameObject), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        // LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        // LuauCore.AddExtensionMethodsFromNamespace(typeof(RectTransform), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(RectTransform), "Easy.Airship", "Code.Extensions");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "Assembly-CSharp", "");
        // LuauCore.AddTypeExtensionMethodsFromClass(typeof(Component), typeof(UnityTweenExtensions));
        // LuauCore.AddTypeExtensionMethodsFromClass(typeof(GameObject), typeof(UnityTweenExtensions));

        SetupUnityAPIClasses();

        foreach (var type in ReflectionList.allowedTypesInternal.Keys) {
            LuauCore.CoreInstance.RegisterComponent(type);
        }
    }

    //This is for things like GameObject:Find() etc - these all get passed to the luau dll on startup
    //Tag the class with [LuauAPI]
    //This works two ways - either derive from BaseLuaAPIClass if you're extending an existing Unity API like GameObject
    //                    - Create a brand-new class and tag it, its members will be automatically reflected
    private void SetupUnityAPIClasses() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies) {
            if (!FastStartsWith(assembly.FullName, "Airship") && !FastStartsWith(assembly.FullName, "Easy")) continue;
            
            // Loop over all types
            foreach (var type in assembly.GetTypes()) {
                // Get custom attributes for type
                var typeAttribute = type.GetCustomAttribute<LuauAPI>(true);
                if (typeAttribute == null) continue;
                
                // Add Luau contextual permissions for the class and methods
                ReflectionList.AddToReflectionList(type, typeAttribute.AllowedContextsMask);
                foreach (var methodInfo in type.GetMethods()) {
                    var methodTypeAttr = methodInfo.GetCustomAttribute<LuauAPI>();
                    if (methodTypeAttr == null) continue;
                    ReflectionList.AddToMethodList(methodInfo, methodTypeAttr.AllowedContextsMask);
                }
                
                if (type.IsSubclassOf(typeof(BaseLuaAPIClass))) {
                    var instance = (BaseLuaAPIClass)Activator.CreateInstance(type);
                    ReflectionList.AddToReflectionList(instance.GetAPIType(), typeAttribute.AllowedContextsMask);
                    LuauCore.CoreInstance.RegisterBaseAPI(instance);
                } else {
                    var customApi = new UnityCustomAPI(type);
                    LuauCore.CoreInstance.RegisterBaseAPI(customApi);
                }
            }
        }
    }

    // Source: https://docs.unity3d.com/Manual/UnderstandingPerformanceStringsAndText.html
    private static bool FastStartsWith(string a, string b) {
        var aLen = a.Length;
        var bLen = b.Length;

        var ap = 0;
        var bp = 0;

        while (ap < aLen && bp < bLen && a[ap] == b[bp]) {
            ap++;
            bp++;
        }

        return bp == bLen;
    }
}
