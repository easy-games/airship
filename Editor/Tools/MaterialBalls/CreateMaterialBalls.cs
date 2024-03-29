using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

public class CreateMaterialBalls : MonoBehaviour {
    [MenuItem("Airship/Tools/Create Material Balls")]
    private static void CreateMaterialBallsButton() {
        
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

        //Red row
        row += 1;
        colorName = "Red";
        color = Color.red;
        dummy = new GameObject("Metal 0% to 100%, Roughness 0%, " + colorName);
                
        for (int col = 0; col < columns; col++) {
            float mx = col / (float)(columns - 1);
            float rx = 0;
            CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
        }
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);

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
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);


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
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);

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
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);

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
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);

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
        dummy.transform.position = Vector3.zero + new Vector3(0, -row * vertSpace, 0);


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
