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

    [MenuItem("GameObject/Airship/Lighting/Lighting Material Balls", false, priorityGroup+3)]
    private static void CreateMaterialBallsButton(MenuCommand menuCommand) {
        
        int columns = 11;
        float spacing = 1.0f;
        float vertSpace = 1.1f;

        // Load the shader
        Shader shader = Shader.Find("Airship/WorldShaderPBR");
        if (shader == null) {
            //Debug.LogError("Shader 'Airship/WorldShaderPBR' not found. Make sure the shader is included in the project.");
            return;
        }

        float row = -1;
        
        string colorName;
        Color color;
        GameObject dummy;
        GameObject holder;
        Vector3 staringPosition = new Vector3(0,6f,0);

        //Red row
        row += 1;
        colorName = "Red";
        color = Color.red;

        // Ensure it gets reparented if this was a context click (otherwise does nothing)
        var parent = menuCommand.context as GameObject;
        if(parent == null && Selection.gameObjects.Length > 0){
            parent = Selection.gameObjects[0];
        }
        holder = new GameObject("Lighting Material Balls");
        GameObjectUtility.SetParentAndAlign(holder, parent);

        dummy = new GameObject("Metal 0% to 100%, Roughness 0%, " + colorName);
                
        for (int col = 0; col < columns; col++) {
            float mx = col / (float)(columns - 1);
            float rx = 0;
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        //Green row
        row += 1;
        colorName = "Green";
        color = Color.green;
        dummy = new GameObject("Metal 0% to 100%, Roughness 100%, " + colorName);
        
        for (int col = 0; col < columns; col++) {
            float mx = col / (float)(columns - 1);
            float rx = 1;
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        //Blue row
        row += 1;
        colorName = "Blue";
        color = Color.blue;
        dummy = new GameObject("Metal 0%, Roughness 0% to 100%, " + colorName);
        
        for (int col = 0; col < columns; col++) {
            float mx = 0;
            float rx = col / (float)(columns - 1);
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.position = staringPosition + new Vector3(0, -row * vertSpace, 0);


        //Yellow row
        row += 1;
        colorName = "Yellow";
        color = Color.yellow;
        dummy = new GameObject("Metal 0%, Roughness 0% to 100%, " + colorName);

        for (int col = 0; col < columns; col++) {
            float mx = 0;
            float rx = col / (float)(columns - 1);
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        //Teal row
        row += 1;
        colorName = "Teal";
        color = Color.cyan;
        dummy = new GameObject("Metal 100%, Roughness 0% to 100%, " + colorName);

        for (int col = 0; col < columns; col++) {
            float mx = 1;
            float rx = col / (float)(columns - 1);
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        //Black row
        row += 1;
        colorName = "Black";
        color = Color.black;
        dummy = new GameObject("Metal 0% to 100%, Roughness 0% to 100%, " + colorName);

        for (int col = 0; col < columns; col++) {
            float mx = col / (float)(columns - 1);
            float rx = col / (float)(columns - 1);
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        //White row
        row += 1;
        colorName = "White";
        color = Color.white;
        dummy = new GameObject("Metal 0% to 100%, Roughness 0% to 100%, " + colorName);

        for (int col = 0; col < columns; col++) {
            float mx = col / (float)(columns - 1);
            float rx = col / (float)(columns - 1);
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.SetParent(holder.transform);
        dummy.transform.localPosition = staringPosition + new Vector3(0, -row * vertSpace, 0);

        // Register the creation in the undo system
        Undo.RegisterCreatedObjectUndo(holder, "Create " + holder.name);
        Selection.activeObject = holder;
    }

    static void CreateBall(float mx, float rx, Color color, string colorName, GameObject dummy, Shader shader, float x) {

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Metal " + (mx * 100).ToString("F0") + "%, Roughness " + (rx * 100).ToString("F0") + "%, " + colorName;
        
        sphere.transform.position = new Vector3(x, 0, 0);
        sphere.transform.parent = dummy.transform;

        // Create a new material instance with the shader
        Material matInstance = new Material(shader);
        sphere.GetComponent<Renderer>().material = matInstance;

        matInstance.SetFloat("_MetalOverride", mx);
        matInstance.SetFloat("_RoughOverride", rx);
        matInstance.SetColor("_Color", color);

        //Grab the renderer and disable shadows
        Renderer rend = sphere.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
 
