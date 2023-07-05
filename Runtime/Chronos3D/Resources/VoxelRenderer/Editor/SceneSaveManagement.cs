#if UNITY_EDITOR
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
            //get every chunk
            foreach (var chunk in voxelWorld.chunks)
            {
        
                chunk.Value.Clear();
            }
        }
    }

    static void OnSceneSaved(Scene scene)
    {
        Debug.Log("Scene saved");
        //put it back
        VoxelWorld[] voxelWorlds = GameObject.FindObjectsOfType<VoxelWorld>();
        foreach (VoxelWorld voxelWorld in voxelWorlds)
        {
            foreach (var chunk in voxelWorld.chunks)
            {
                chunk.Value.SetGeometryDirty(true);
            }
        }
    }
}
#endif