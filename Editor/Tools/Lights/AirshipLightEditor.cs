using System.IO;
using Airship;
using UnityEditor;
using UnityEngine;

public class AirshipLightEditor : MonoBehaviour {
    private const int priorityGroup = -500;
    
    [MenuItem("GameObject/Airship/Lighting/Lighting Render Settings", false, priorityGroup+2)]
    static void CreateRenderSettings(MenuCommand menuCommand)
    {
        //Create a gameobject and stick a light component on it
        var go = new GameObject("Lighting Render Settings");
        //Add undo support
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        var renderSettings = go.AddComponent<AirshipRenderSettings>();
        renderSettings.GetCubemapFromScene();
        
        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        var parent = menuCommand.context as GameObject;
        if(parent == null && Selection.gameObjects.Length > 0){
            parent = Selection.gameObjects[0];
        }
        GameObjectUtility.SetParentAndAlign(go, parent);
        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }

   
}
 
