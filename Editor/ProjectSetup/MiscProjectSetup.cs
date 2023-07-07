using System;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class MiscProjectSetup : MonoBehaviour
{
    private void Start()
    {
        Setup();
    }

    public void Setup()
    {
        var editorConfig = AssetDatabase.LoadAssetAtPath<EasyEditorConfig>("Assets/EasyEditorConfig.asset");
        if (editorConfig == null)
        {
            var newConfig = ScriptableObject.CreateInstance<EasyEditorConfig>();
            AssetDatabase.CreateAsset(newConfig, "Assets/EasyEditorConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Physics.gravity = new Vector3(0, -164.808f, 0);
    }
}