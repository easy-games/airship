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
        go.AddComponent<AirshipPointLight>();
    }
}
