using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
#endif
public class SetupManager : AssetPostprocessor{

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths){
#if UNITY_EDITOR
        if (!SessionState.GetBool("FirstAirshipSettingsInitDone", false)) {
            // Startup code here
            FixProject();
         
            SessionState.SetBool("FirstAirshipSettingsInitDone", true);
        }
#endif
    }
    

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