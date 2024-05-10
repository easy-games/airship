using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
[InitializeOnLoad]
#endif
public static class SetupManager{
    static SetupManager() {
        if (!SessionState.GetBool("FirstAirshipSettingsInitDone", false)) {
            // Startup code here
            FixProject();
         
            SessionState.SetBool("FirstAirshipSettingsInitDone", true);
        }
    }

//     static SetupManager() {
// #if UNITY_EDITOR
//         FixProject();
// #endif
//     }

#if UNITY_EDITOR
    [MenuItem("Airship/Misc/Repair Project")]
    #endif
    public static void FixProject()
    {
#if UNITY_EDITOR
        Debug.Log("Setting up Airship Project Settings");
        FishNetSetup.Setup();
        var config = MiscProjectSetup.Setup();
        PhysicsSetup.Setup(config);
#endif
    }
}