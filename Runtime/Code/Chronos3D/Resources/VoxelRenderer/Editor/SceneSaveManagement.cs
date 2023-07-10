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
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving; /// <-----------------
    }

    static void OnSceneSaving(Scene scene, string path)
    {
        UnityEngine.Debug.LogFormat("Saving scene '{0}'", scene.name);

        //get every voxelWorld
        VoxelWorld[] voxelWorlds = GameObject.FindObjectsOfType<VoxelWorld>();
        foreach (VoxelWorld voxelWorld in voxelWorlds)
        {
            foreach (var pair in voxelWorld.chunks)
            {
                pair.Value.Clear();
            }

            List<GameObject> toDestroy = new();
            for (int i = 0; i < voxelWorld.transform.childCount; i++)
            {
                var t = voxelWorld.transform.GetChild(i);
                toDestroy.Add(t.gameObject);
            }

            foreach (var go in toDestroy)
            {
                Object.DestroyImmediate(go);
            }
            EditorUtility.SetDirty(voxelWorld.gameObject);
        }
    }

    static void OnSceneSaved(Scene scene)
    {
        Debug.Log("Scene saved");
        //put it back
        // VoxelWorld[] voxelWorlds = GameObject.FindObjectsOfType<VoxelWorld>();
        // foreach (VoxelWorld voxelWorld in voxelWorlds)
        // {
        //     voxelWorld.LoadWorldFromVoxelBinaryFile(voxelWorld.voxelWorldFile, voxelWorld.blockDefines);
        // }
    }
}
#endif