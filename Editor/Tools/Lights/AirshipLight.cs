using System.IO;
using UnityEditor;
using UnityEngine;

public class AirshipLightEditor : MonoBehaviour {
    private const int priorityGroup = -100;
    
    [MenuItem("GameObject/Light/Airship Pointlight", false, priorityGroup)]
    static void CreatePointLight(MenuCommand menuCommand)
    {
        //Create a gameobject and stick a light component on it
        var go = new GameObject("AirshipPointlight");
        //Add undo support
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        go.AddComponent<AirshipPointLight>();

    }

    [MenuItem("GameObject/Light/Airship VoxelLight", false, priorityGroup)]
    static void CreateVoxelLight(MenuCommand menuCommand)
    {
        //Create a gameobject and stick a light component on it
        var go = new GameObject("AirshipVoxelLight");
        //Add undo support
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        go.AddComponent<AirshipVoxelLight>();
    }
}
 
