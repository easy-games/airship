using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.IO;
using UnityEditor.AssetImporters;
using System.Reflection;
using System;
using static UnityEngine.GraphicsBuffer;

public class ImageImportSettings : AssetPostprocessor
{
    
    private void OnPostprocessTexture(Texture2D texture) {

        if (ARPConfig.IsDisabled) return;
        
        //Load the serialized Data
        CustomTextureSettingsData userData = new CustomTextureSettingsData();
        if (assetImporter.userData.Length > 0) {
            userData = JsonUtility.FromJson<CustomTextureSettingsData>(assetImporter.userData);
        }
 
        if (userData.isMetalOrRoughnessMap) {
          
            //Log that we're running
            Debug.Log("Data Channel Texture: " + assetPath);
            
            Color[] pixels = texture.GetPixels();

            float c = 0.4545454f;
            //Run a pow function on every pixel (pow(x, 0.45454545)
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i].r = Mathf.Clamp01(Mathf.Pow(pixels[i].r, c));
                pixels[i].g = Mathf.Clamp01(Mathf.Pow(pixels[i].g, c));
                pixels[i].b = Mathf.Clamp01(Mathf.Pow(pixels[i].b, c));
                
            }
    
            texture.SetPixels(pixels);
            texture.Apply();
        }

        if (userData.brightness != 1.0) {
            //adjust texture brightness
            Color[] pixels = texture.GetPixels();
            
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i].r = Mathf.Clamp01(pixels[i].r * userData.brightness);
                pixels[i].g = Mathf.Clamp01(pixels[i].g * userData.brightness);
                pixels[i].b = Mathf.Clamp01(pixels[i].b * userData.brightness);
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }
                
    }
    /*
   private void OnPostprocessCubemap() {
     
       TextureImporter importer = assetImporter as TextureImporter;

       TextureImporterSettings src = new TextureImporterSettings();
       src.filterMode = FilterMode.Trilinear;
       src.mipmapEnabled = true;
       src.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
       src.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
       //src.sRGBTexture = true;
       importer.SetTextureSettings(src);
     }
    
    */
}

[Serializable]
public class CustomTextureSettingsData {

    public bool isMetalOrRoughnessMap = false;
    public float brightness = 1.0f;
    
}

[CustomEditor(typeof(TextureImporter))]
public class TextureImporterCustomEditor : UnityEditor.Editor {
    private SerializedObject serializedTarget;

    private AssetImporterEditor nativeEditor;
    private CustomTextureSettingsData userData = new CustomTextureSettingsData();

    public void OnEnable() {
     

        serializedTarget = new SerializedObject(target);
        SceneView.onSceneGUIDelegate = TargetUpdate;

        Type t = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            foreach (Type type in assembly.GetTypes()) {
                if (type.Name.ToLower().Contains("textureimporterinspector")) {
                    t = type;
                    break;
                }
            }
        }

        nativeEditor = (AssetImporterEditor)UnityEditor.Editor.CreateEditor(serializedObject.targetObject, t);
        
        //Get the InternalSetAssetImporterTargetEditor method using reflection
        MethodInfo method = nativeEditor.GetType().GetMethod("InternalSetAssetImporterTargetEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method != null) {
            // Assuming the method does not take any parameters and you're invoking it on 'nativeEditor'
            
            method.Invoke(nativeEditor, new object[] {this });
        }
    }

    void TargetUpdate(SceneView sceneview) {
        
        Event e = Event.current;

        if (serializedObject.targetObject) {
            //deserialize our userdata
            string data = ((TextureImporter)serializedObject.targetObject).userData;

            if (data != null) {
                JsonUtility.FromJsonOverwrite(data, userData);
            } 
        }

    }

    public override void OnInspectorGUI() {
        
        if (nativeEditor != null) {
            nativeEditor.OnInspectorGUI();

            if (ARPConfig.IsDisabled) {
              
                return;
            }

            //Add a bool for isRoughness
            EditorGUI.BeginChangeCheck();


            //Start a box
            EditorGUILayout.BeginVertical("box");
            //Add title
            EditorGUILayout.LabelField("Airship Custom Texture Settings", EditorStyles.boldLabel);

            //Add divider
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            //Roughness/Metal (automatically apply pow 0.45454545)
            userData.isMetalOrRoughnessMap = EditorGUILayout.Toggle("Is Metal or Roughness Map", userData.isMetalOrRoughnessMap);
            
            //brightness slider
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Adjust Brightness", EditorStyles.label);
            userData.brightness = EditorGUILayout.Slider(userData.brightness, 0, 4);
            EditorGUILayout.EndHorizontal();

            //end box
            EditorGUILayout.EndVertical();

            //On changed?
            if (EditorGUI.EndChangeCheck()) {
                //Reserialize
                ((TextureImporter)serializedObject.targetObject).userData = JsonUtility.ToJson(userData);
                
            }
                        
            GUILayout.Space(2048);

            serializedObject.ApplyModifiedProperties();
        }
    }
    protected override void OnHeaderGUI() {
        
    }
}

