using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class BuildAssetBundlesOnPlay
{
    [InitializeOnEnterPlayMode]
    static void OnEnterPlaymodeInEditor(EnterPlayModeOptions options)
    {
        // Debug.Log("[EDITOR]: Building asset bundles...");
        // CreateAssetBundles.BuildLocalAssetBundles();
    }
}