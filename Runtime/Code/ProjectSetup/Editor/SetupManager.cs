#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class SetupManager : AssetPostprocessor {
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths) {
        if (!SessionState.GetBool("FirstAirshipSettingsInitDone", false)) {
            // Startup code here
            FixProject();

            SessionState.SetBool("FirstAirshipSettingsInitDone", true);
        }
    }

    [MenuItem("Airship/Misc/Repair Project")]
    public static void FixProject() {
        PhysicsSetup.Setup();
    }

    [MenuItem("Airship/Misc/Reset Physics To Airship Defaults")]
    public static void ResetPhysics() {
        PhysicsSetup.ResetDefaults();
    }
}
#endif