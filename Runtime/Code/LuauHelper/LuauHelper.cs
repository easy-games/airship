using System;
using UnityEngine;

public class LuauHelper : Singleton<LuauHelper> {
    public static bool didReflection = false;

    private void OnEnable() {
        LuauCore.onSetupReflection += this.LuauCore_OnSetupReflection;
    }

    private void OnDisable() {
        LuauCore.onSetupReflection -= this.LuauCore_OnSetupReflection;
    }

    private void LuauCore_OnSetupReflection() {
        if (didReflection) return;
        didReflection = true;
        LuauCore.AddExtensionMethodsFromNamespace(typeof(GameObject), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "nl.elraccoone.tweens", "ElRaccoone.Tweens");
        LuauCore.AddExtensionMethodsFromNamespace(typeof(Component), "Assembly-CSharp", "");
        LuauCore.AddTypeExtensionMethodsFromClass(typeof(Component), typeof(UnityTweenExtensions));
        LuauCore.AddTypeExtensionMethodsFromClass(typeof(GameObject), typeof(UnityTweenExtensions));
    }
}