using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Profiling;
using static UnityEngine.LightingSettings;

public class AirshipLightmapManager {

    private static bool doMaterialSwap = false;
    private static AirshipLightmapManager instance;
    public static AirshipLightmapManager Instance {
        get {
            if (instance == null) {
                instance = new AirshipLightmapManager();
            }
            return instance;
        }
    }

    //constructor
    private AirshipLightmapManager() {
#if UNITY_EDITOR
        
        Lightmapping.bakeStarted += OnBakeStarted;
        //The bakeCompleted event does not get fired if the user cancels. This is a disaster for us, so we'll check for the bake ending manually every frame
        //Lightmapping.bakeCompleted += OnBakeCompleted;
#endif        
    }
    private Dictionary<Renderer, Material[]> originalMaterials = new();
    private void OnBakeStarted() {
        
        if (ARPConfig.IsDisabled) return;
        //So, as of unity 2023.2.3f1
        //The lightmap baker does not respect material property blocks for setting stuff like _Color
        //So, the workaround attempts to swap the material on everything for the duration of the bake
        //And then swap it back afterwards

        if (doMaterialSwap == false) {
            return;
        }
        originalMaterials.Clear();
        
        Renderer[] renderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers) {

            Material[] replacementMaterials = new Material[renderer.sharedMaterials.Length];
            
            //Stash the original list
            originalMaterials[renderer] = renderer.sharedMaterials;

            
            MaterialColor materialColors = renderer.GetComponent<MaterialColor>();
            if (materialColors == null) {
                continue;
            }

            int index = 0;
            foreach (Material material in renderer.sharedMaterials) {
                if (material == null) {
                    index += 1;
                    continue;
                }
                if (material.shader.name == "Airship/WorldShaderPBR") {
                    
                    //clone it
                    Material newMaterial = new Material(material);
                    replacementMaterials[index] = newMaterial;

                    //Set the color
                    newMaterial.SetColor("_Color", materialColors.GetColorSettings(index).materialColor);
                   
                }
                else {
                    //Pass through
                    replacementMaterials[index] = material;
                }
                index += 1;
            }
            renderer.sharedMaterials = replacementMaterials;
        }
    }

    private void OnBakeCompleted() {


        if (doMaterialSwap == false) {
            return;
        }
        Debug.Log("Bake completed, restoring materials");

        foreach (var kvp in originalMaterials) {

            Renderer renderer = kvp.Key;

            //Make sure renderer isnt destroyed
            if (renderer == null) {
                continue;
            }
            renderer.sharedMaterials = kvp.Value;
        }
        originalMaterials.Clear();
    }

    public void OnRender() {
#if UNITY_EDITOR
        //See if a bake has ended since the last time we checked
        if (Lightmapping.isRunning == false && originalMaterials.Count > 0) {
            OnBakeCompleted();
        }
#endif        
    }
 


}
