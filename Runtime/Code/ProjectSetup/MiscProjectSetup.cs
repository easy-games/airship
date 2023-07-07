#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public static class MiscProjectSetup
{

    public static void Setup()
    {
#if UNITY_EDITOR
        var editorConfig = AssetDatabase.LoadAssetAtPath<EasyEditorConfig>("Assets/EasyEditorConfig.asset");
        if (editorConfig == null)
        {
            var newConfig = ScriptableObject.CreateInstance<EasyEditorConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/EasyEditorConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Physics.gravity = new Vector3(0, -54.936f, 0);

        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
#endif
    }
}