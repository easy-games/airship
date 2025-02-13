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
        var config = MiscProjectSetup.Setup();
        PhysicsSetup.Setup(config);
    }

    [MenuItem("Airship/Misc/Reset Physics To Airship Defaults")]
    public static void ResetPhysics() {
        var config = MiscProjectSetup.GetOrCreateGameConfig();
        PhysicsSetup.ResetDefaults(config, PhysicsSetup.defaultGravity);
    }
}
#endif