using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

public class CreateMaterialBalls : MonoBehaviour {
    // [MenuItem("Airship/Tools/Create Material Balls")]
    // private static void CreateMaterialBallsButton() {
    //
    //     int columns = 7;
    //     float spacing = 1.4f;
    //     float vertSpace = 1.4f;
    //
    //     // Load the shader
    //     Shader shader = Shader.Find("Airship/WorldShaderPBR");
    //     if (shader == null) {
    //         //Debug.LogError("Shader 'Airship/WorldShaderPBR' not found. Make sure the shader is included in the project.");
    //         return;
    //     }
    //
    //     //Get the current camera
    //     Camera cam = Camera.current;
    //
    //     Vector3 baseVec = cam.transform.position + cam.transform.forward * 15.0f;
    //
    //     float row = -1;
    //
    //     string colorName;
    //     Color color;
    //     GameObject dummy;
    //
    //     for (int rowz = 0; rowz < columns; rowz++) {
    //         row += 1;
    //         colorName = "White";
    //         color = Color.white;
    //         dummy = new GameObject("Substance Calibration " + colorName);
    //
    //         for (int col = 0; col < columns; col++) {
    //             float rx = (col / (float)(columns - 1));
    //             float mx = 1-(rowz / (float)(columns - 1));
    //             CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
    //         }
    //         dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);
    //
    //     }
    //
    //     for (int rowz = 0; rowz < columns; rowz++) {
    //         row += 1;
    //         colorName = "Red";
    //         color = Color.red;
    //         dummy = new GameObject("Substance Calibration " + colorName);
    //
    //         for (int col = 0; col < columns; col++) {
    //             float rx = (col / (float)(columns - 1));
    //             float mx = 1 - (rowz / (float)(columns - 1));
    //             CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
    //         }
    //         dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);
    //
    //     }
    //
    //     row = -1;
    //     baseVec += new Vector3((columns  ) * spacing, 0, 0);
    //
    //     for (int rowz = 0; rowz < columns; rowz++) {
    //         row += 1;
    //         colorName = "Green";
    //         color = Color.green;
    //         dummy = new GameObject("Substance Calibration " + colorName);
    //
    //         for (int col = 0; col < columns; col++) {
    //             float rx = (col / (float)(columns - 1));
    //             float mx = 1 - (rowz / (float)(columns - 1));
    //             CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
    //         }
    //         dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);
    //
    //     }
    //
    //     for (int rowz = 0; rowz < columns; rowz++) {
    //         row += 1;
    //         colorName = "Blue";
    //         color = Color.blue;
    //         dummy = new GameObject("Substance Calibration " + colorName);
    //
    //         for (int col = 0; col < columns; col++) {
    //             float rx = (col / (float)(columns - 1));
    //             float mx = 1 - (rowz / (float)(columns - 1));
    //             CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
    //         }
    //         dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);
    //
    //     }
    //
    // }
    //
    // static void CreateBall(float mx, float rx, Color color, string colorName, GameObject dummy, Shader shader, float x) {
    //
    //     GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //     sphere.name = "Metal " + (mx * 100).ToString("F0") + "%, Roughness " + (rx * 100).ToString("F0") + "%, " + colorName;
    //
    //     sphere.transform.position = new Vector3(x, 0, 0);
    //     sphere.transform.parent = dummy.transform;
    //
    //     // Create a new material instance with the shader
    //     Material matInstance = new Material(shader);
    //     sphere.GetComponent<Renderer>().material = matInstance;
    //
    //     matInstance.SetFloat("_MRSliderOverrideMix", 1);
    //     matInstance.SetFloat("_MetalOverride", mx);
    //     matInstance.SetFloat("_RoughOverride", rx);
    //     matInstance.SetColor("_Color", color);
    //
    //     //Grab the renderer and disable shadows
    //     Renderer rend = sphere.GetComponent<Renderer>();
    //     rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    // }

}
