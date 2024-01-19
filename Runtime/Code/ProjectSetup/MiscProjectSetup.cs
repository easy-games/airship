#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

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
        GraphicsSettings.videoShadersIncludeMode = VideoShadersIncludeMode.Never;
        ClearIncludedShader();
        // EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.Standalone, GraphicsTier.Tier2, null);

#endif
#endif
    }

    /*
     * Source: https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
     */
    private static void ClearIncludedShader() {
        var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var serializedObject = new SerializedObject(graphicsSettingsObj);
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
        arrayProp.ClearArray();

        serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}