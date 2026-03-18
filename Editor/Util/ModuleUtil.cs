using UnityEditor;
using UnityEngine;

public class ModuleUtil {

    public static bool IsModuleInstalled(BuildTarget buildTarget) {
        // Reference: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Modules/ModuleManager.cs
        // Super hacky reflection... Not sure how reliable this is.
        var moduleManager = System.Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
        if (moduleManager == null) {
            Debug.LogWarning("[Airship] No module manager found when checking platform install. Report this.");
            return false;
        }
        var isPlatformSupportLoaded = moduleManager.GetMethod(
                "IsPlatformSupportLoaded",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var getTargetStringFromBuildTarget = moduleManager.GetMethod(
                "GetTargetStringFromBuildTarget", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (isPlatformSupportLoaded == null) {
            Debug.LogWarning("[Airship] IsPlatformSupportLoaded method not found. Report this.");
            return false;
        }
        if (getTargetStringFromBuildTarget == null) return false;
        var targetString = (string) getTargetStringFromBuildTarget.Invoke(null, new object[] { buildTarget });
        return (bool) isPlatformSupportLoaded.Invoke(null,new object[] { targetString });
    }
        
}
