#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class RefreshAssetsOnPlayModeExit
{
    static RefreshAssetsOnPlayModeExit()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Check if the editor has just exited play mode
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Debug.Log("Exited Play Mode. Refreshing Assets...");
            AssetDatabase.Refresh();
        }
    }
}
#endif