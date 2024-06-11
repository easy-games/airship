using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

public class CreateMaterialBalls : MonoBehaviour {
    [MenuItem("Airship/Tools/Create Material Balls")]
    private static void CreateMaterialBallsButton() {
        
        int columns = 7;
        float spacing = 1.4f;
        float vertSpace = 1.4f;

        // Load the shader
        Shader shader = Shader.Find("Airship/WorldShaderPBR");
        if (shader == null) {
            //Debug.LogError("Shader 'Airship/WorldShaderPBR' not found. Make sure the shader is included in the project.");
            return;
        }

        //Get the current camera
        Camera cam = Camera.current;

        Vector3 baseVec = cam.transform.position + cam.transform.forward * 15.0f;

        float row = -1;
        
        string colorName;
        Color color;
        GameObject dummy;
 
        for (int rowz = 0; rowz < columns; rowz++) {
            row += 1;
            colorName = "White";
            color = Color.white;
            dummy = new GameObject("Substance Calibration " + colorName);

            for (int col = 0; col < columns; col++) {
                float rx = (col / (float)(columns - 1));
                float mx = 1-(rowz / (float)(columns - 1));
                CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
            }
            dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);
            
        }

        for (int rowz = 0; rowz < columns; rowz++) {
            row += 1;
            colorName = "Red";
            color = Color.red;
            dummy = new GameObject("Substance Calibration " + colorName);

            for (int col = 0; col < columns; col++) {
                float rx = (col / (float)(columns - 1));
                float mx = 1 - (rowz / (float)(columns - 1));
                CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
            }
            dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);

        }

        row = -1;
        baseVec += new Vector3((columns  ) * spacing, 0, 0);
        
        for (int rowz = 0; rowz < columns; rowz++) {
            row += 1;
            colorName = "Green";
            color = Color.green;
            dummy = new GameObject("Substance Calibration " + colorName);

            for (int col = 0; col < columns; col++) {
                float rx = (col / (float)(columns - 1));
                float mx = 1 - (rowz / (float)(columns - 1));
                CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
            }
            dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);

        }

        for (int rowz = 0; rowz < columns; rowz++) {
            row += 1;
            colorName = "Blue";
            color = Color.blue;
            dummy = new GameObject("Substance Calibration " + colorName);

            for (int col = 0; col < columns; col++) {
                float rx = (col / (float)(columns - 1));
                float mx = 1 - (rowz / (float)(columns - 1));
                CreateBall(mx, rx, color, colorName, dummy, shader, col * spacing);
            }
            dummy.transform.position = baseVec + new Vector3(0, -row * vertSpace, 0);

        }

    }

     

}
