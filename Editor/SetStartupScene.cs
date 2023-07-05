using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class EditorInit
{
    static EditorInit()
    {
        // var pathOfFirstScene = EditorBuildSettings.scenes[0].path;
        // var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathOfFirstScene);
        // EditorSceneManager.playModeStartScene = sceneAsset;
        // Debug.Log(pathOfFirstScene + " was set as default play mode scene");
        
    }
}