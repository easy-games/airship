using System;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class MiscProjectSetup : MonoBehaviour
{
    private void Start()
    {
        var editorConfig = AssetDatabase.LoadAssetAtPath<EasyEditorConfig>("Assets/EasyEditorConfig.asset");
        if (editorConfig == null)
        {
            var newConfig = ScriptableObject.CreateInstance<EasyEditorConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/EasyEditorConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}