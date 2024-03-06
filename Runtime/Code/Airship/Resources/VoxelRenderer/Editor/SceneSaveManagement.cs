#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class EditorSceneManagenent
{
    static EditorSceneManagenent()
    {
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    static void OnSceneSaving(Scene scene, string path) {
        if (Application.isPlaying) return;
        // UnityEngine.Debug.LogFormat("Saving scene '{0}'", scene.name);

        //get every voxelWorld
        // VoxelWorld[] voxelWorlds = GameObject.FindObjectsOfType<VoxelWorld>();
        // foreach (VoxelWorld voxelWorld in voxelWorlds)
        // {
        //     if (voxelWorld.chunks.Count > 0) {
        //         voxelWorld.SaveToFile();
        //     }
        // }
    }

    static void OnSceneSaved(Scene scene) {
        // Debug.Log("Scene saved");
    }
}
#endif