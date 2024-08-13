#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class SetupManager : AssetPostprocessor{

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths){
        if (!SessionState.GetBool("FirstAirshipSettingsInitDone", false)) {
            // Startup code here
            FixProject();
         
            SessionState.SetBool("FirstAirshipSettingsInitDone", true);
        }
    }
    
    [MenuItem("Airship/Misc/Repair Project")]
    public static void FixProject()
    {
        FishNetSetup.Setup();
        var config = MiscProjectSetup.Setup();
        PhysicsSetup.Setup(config);
    }
}
#endif