#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public static class MiscProjectSetup
{
    public static GameConfig Setup() {

#if UNITY_EDITOR
        GameConfig gameBundleConfig = GetOrCreateGameConfig();

        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

#if !AIRSHIP_PLAYER
        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
        var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
        PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);

        GraphicsSettings.videoShadersIncludeMode = VideoShadersIncludeMode.Never;

        var values = Enum.GetValues(typeof(BuiltinShaderType)).Cast<BuiltinShaderType>();
        foreach (var val in values) {
            if (val == BuiltinShaderType.LegacyDeferredLighting) continue;
            GraphicsSettings.SetShaderMode(val, BuiltinShaderMode.Disabled);
        }

        PlayerSettings.stripUnusedMeshComponents = false;

        ClearIncludedShader();
#endif
        return gameBundleConfig;
#endif
        return null;
    }

    /// <summary>
    /// Editor only
    /// </summary>
    public static GameConfig GetOrCreateGameConfig(){
#if UNITY_EDITOR
        var gameBundleConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
        if (gameBundleConfig == null)
        {
            Debug.Log("Creating new game config file at \"Assets/GameConfig.asset\"");
            var newConfig = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/GameConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        return gameBundleConfig;
#else
        return null;
#endif
    }

    /*
     * Source: https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
     */
    private static void ClearIncludedShader() {
#if UNITY_EDITOR
        var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var serializedObject = new SerializedObject(graphicsSettingsObj);
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
        arrayProp.ClearArray();

        serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
#endif
    }
}