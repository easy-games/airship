#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public static class MiscProjectSetup
{

    public static void Setup()
    {
#if UNITY_EDITOR
        var editorConfig = AssetDatabase.LoadAssetAtPath<AirshipEditorConfig>("Assets/AirshipEditorConfig.asset");
        if (editorConfig == null)
        {
            var newConfig = ScriptableObject.CreateInstance<AirshipEditorConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/AirshipEditorConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var gameBundleConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
        if (gameBundleConfig == null)
        {
            var newConfig = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/GameConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Physics.gravity = new Vector3(0, -54.936f, 0);

        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

#if !AIRSHIP_PLAYER
        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
        var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
        PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);
#endif
#endif
    }
}