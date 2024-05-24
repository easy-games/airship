using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR

public class AirshipWorldShaderInspector : ShaderGUI {

    bool showTextureInputs = true;
    bool showSurfaceFeatures = true;
    bool showUVs = true;
    bool showRimLighting = true;
    bool showEmission = true;
    bool showMetalRoughnessSliders = true;

    static Texture2D tex;
    private Texture2D MakeTex(int width, int height, Color col) {
        if (tex == null) {
            
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            tex = result;
            return result;
        }
        return tex;
    }

    public enum SurfaceType {
        Opaque, TransparentAlphaBlend, TransparentAdd, AlphaCutout
    }
    public enum UVMode {
        UVMapped, LocalSpaceTriplanar, WorldSpaceTriplanar
    }

  

    public bool GetMatColor(Material mat, out MaterialColor returnedMatColor, out int index) {

        returnedMatColor = null;
        index = -1;
        
        //get the currently selected unity gameobject
        GameObject go = Selection.activeGameObject;
        if (go == null){
            return false;
        }
        //Get the renderer component
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) {
            return false;
        }

        //See if the renderer has this material
        int found = -1;
        Material[] mats = renderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) {
            if (mats[i] == mat) {
                found = i;
                break;
            }
        }
        if (found == -1) {
            return false;
        }

        MaterialColor matColor = go.GetComponent<MaterialColor>();
        if (matColor == null) {
            return false;
        }
        returnedMatColor = matColor;
        index = found;

        return true;
        
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {

        //Find the MaterialColor component on this object
        Material material = materialEditor.target as Material;

        bool hasMatColor = GetMatColor(material, out MaterialColor matColor, out int materialIndex);
        GUIStyle foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader);
        foldoutHeaderStyle.normal.background = MakeTex(1200, 1, new Color(0.2f, 0.2f, 0.2f));
        foldoutHeaderStyle.onNormal.background = foldoutHeaderStyle.normal.background;
        foldoutHeaderStyle.fixedWidth = 1200;

      
        GUIStyle greenLabelStyle = new GUIStyle(GUI.skin.label);
        greenLabelStyle.normal.textColor = new Color(0.5f, 0.7f, 0.5f);
        
        MaterialProperty mainTexProp = FindProperty("_MainTex", properties);
        MaterialProperty normalTexProp = FindProperty("_NormalTex", properties);
        MaterialProperty metalTexProp = FindProperty("_MetalTex", properties);
        MaterialProperty roughTexProp = FindProperty("_RoughTex", properties);
        MaterialProperty emissiveMaskProp = FindProperty("_EmissiveMaskTex", properties);
        MaterialProperty colorMaskProp = FindProperty("_ColorMaskTex", properties);
        MaterialProperty uvStyle = FindProperty("TRIPLANAR_STYLE", properties);
        MaterialProperty extraFeaturesProp = FindProperty("EXTRA_FEATURES", properties);
        MaterialProperty surfaceProp = FindProperty("_SurfaceType", properties);

        MaterialProperty rimColorProp = FindProperty("_RimColor", properties);
        MaterialProperty rimPowerProp = FindProperty("_RimPower", properties);
        MaterialProperty rimIntensityProp = FindProperty("_RimIntensity", properties);

        MaterialProperty MRSliderOverrideMixProp = FindProperty("_MRSliderOverrideMix", properties);
        MaterialProperty roughOverrideProp = FindProperty("_RoughOverride", properties);
        MaterialProperty metalOverrideProp = FindProperty("_MetalOverride", properties);

        MaterialProperty triplanarScaleProp = FindProperty("_TriplanarScale", properties);

        // Start the custom inspector
        EditorGUI.BeginChangeCheck();
        
        Rect rect = GUILayoutUtility.GetRect(18, 18, foldoutHeaderStyle);
        
        showSurfaceFeatures = EditorGUI.Foldout(rect, showSurfaceFeatures, "Surface Features",true, foldoutHeaderStyle);

        if (showSurfaceFeatures) {
            
            surfaceProp.floatValue = (int)(SurfaceType)EditorGUILayout.EnumPopup("Surface Type", (SurfaceType)surfaceProp.floatValue);
            
            extraFeaturesProp.floatValue = EditorGUILayout.Popup("Features", (int)extraFeaturesProp.floatValue, new string[] { "Standard", "Extended" });
            switch (extraFeaturesProp.floatValue) {
                case 0:
                material.DisableKeyword("EXTRA_FEATURES_ON");
                break;
                case 1:
                material.EnableKeyword("EXTRA_FEATURES_ON");
                break;
            }

            //Normals
            MaterialProperty doubleSidedProp = FindProperty("DOUBLE_SIDED_NORMALS", properties);  //Where no == 2, and yes == 0

            // Get the current value and map it to the popup index (0 for Yes, 1 for No)
            int currentIndex = (doubleSidedProp.floatValue == 2f) ? 0 : 1;

            // Show the popup and get the new selected index
            currentIndex = EditorGUILayout.Popup("Double Sided", currentIndex, new string[] { "No", "Yes" });

            // Map the selected index back to the float value (0 for Yes, 2 for No)
            doubleSidedProp.floatValue = (currentIndex == 0) ? 2f : 0f;
            
            SurfaceType surface = (SurfaceType)material.GetFloat("_SurfaceType");
            if (surface != SurfaceType.Opaque) {

                //Alpha
                MaterialProperty alphaProp = FindProperty("_Alpha", properties);
                //Use a slider
                alphaProp.floatValue = EditorGUILayout.Slider("Alpha", alphaProp.floatValue, 0.0f, 1.0f);

                if (surface == SurfaceType.AlphaCutout) {
                    //Add a hint
                    EditorGUILayout.HelpBox("Alpha Cutout occurs at 0.5", MessageType.Info);
                }
            }

            switch (surface) {
                case SurfaceType.Opaque:
                material.renderQueue = (int)RenderQueue.Geometry;
                material.SetOverrideTag("RenderType", "Opaque");
                break;
                case SurfaceType.AlphaCutout:
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                break;
                case SurfaceType.TransparentAlphaBlend:
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
                break;
                case SurfaceType.TransparentAdd:
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
                break;
            }

            switch (surface) {
                case SurfaceType.Opaque:
                case SurfaceType.AlphaCutout:
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                break;
                case SurfaceType.TransparentAlphaBlend:
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                break;
                case SurfaceType.TransparentAdd:
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.One);
                material.SetInt("_ZWrite", 0);
                break;
            }

            if (surface == SurfaceType.AlphaCutout) {
                material.EnableKeyword("ALPHA_CUTOUT_ON");
            }
            else {
                material.DisableKeyword("ALPHA_CUTOUT_ON");
            }
            
            material.SetShaderPassEnabled("ShadowCaster", surface != SurfaceType.TransparentAlphaBlend && surface != SurfaceType.TransparentAdd);

            //ZWrite
            //MaterialProperty zWriteProp = FindProperty("_ZWrite", properties);
            //zWriteProp.floatValue = EditorGUILayout.Popup("ZWrite", (int)zWriteProp.floatValue, new string[] { "Off", "On" });
        }
 
        showTextureInputs = EditorGUILayout.Foldout(showTextureInputs, "Surface Inputs", true, foldoutHeaderStyle);
        
        if (showTextureInputs) {

            if (hasMatColor) {
                //Show the color picker
                EditorGUI.BeginChangeCheck();
                //Change the ui text to green

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Color", greenLabelStyle);

                MaterialColor.ColorSetting settings = matColor.GetColorSettings(materialIndex);
                Color newColor = EditorGUILayout.ColorField(settings.materialColor);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(matColor, "Color Changed");
                    settings.materialColor = newColor;
                    matColor.SetColorSettings(materialIndex, settings);
                }
                EditorGUILayout.EndHorizontal();

            }
            materialEditor.TexturePropertySingleLine(new GUIContent("Main Texture"), mainTexProp);

            if (extraFeaturesProp.floatValue == 1) {
                materialEditor.TexturePropertySingleLine(new GUIContent("Color Mask Texture"), colorMaskProp);
            }
            
            materialEditor.TexturePropertySingleLine(new GUIContent("Normal Texture"), normalTexProp);
            materialEditor.TexturePropertySingleLine(new GUIContent("Metal Texture"), metalTexProp);
            materialEditor.TexturePropertySingleLine(new GUIContent("Rough Texture"), roughTexProp);
 
            if (MRSliderOverrideMixProp.floatValue > 0.99) {
                //warn
                EditorGUILayout.HelpBox("The metal and roughness textures will not contribute as the metal/rough sliders are set to 1", MessageType.Warning);
            }
        }

        //property drawer for uvs
        showUVs = EditorGUILayout.Foldout(showUVs, "Surface UVs", true, foldoutHeaderStyle);
        if (showUVs) {
         
            uvStyle.floatValue = (int)(UVMode)EditorGUILayout.EnumPopup("UV Style", (UVMode)uvStyle.floatValue);
            switch (uvStyle.floatValue) {
                case 0:
                material.DisableKeyword("TRIPLANAR_STYLE_WORLD");
                material.DisableKeyword("TRIPLANAR_STYLE_LOCAL");
                break;
                case 1:
                material.EnableKeyword("TRIPLANAR_STYLE_WORLD");
                material.DisableKeyword("TRIPLANAR_STYLE_LOCAL");
                break;
                case 2:
                material.DisableKeyword("TRIPLANAR_STYLE_WORLD");
                material.EnableKeyword("TRIPLANAR_STYLE_LOCAL");
                break;
            } 
            if (uvStyle.floatValue != 0) {
                
                //slider for triplanarScale
                triplanarScaleProp.floatValue = EditorGUILayout.Slider("Triplanar Scale", triplanarScaleProp.floatValue,0.001f, 16.0f);
            }
        }


        if (extraFeaturesProp.floatValue == 1) {
            //Property drawer for rim shading
            
            showRimLighting = EditorGUILayout.Foldout(showRimLighting, "Rim Shading", true, foldoutHeaderStyle);
            if (showRimLighting) {
                rimIntensityProp.floatValue = EditorGUILayout.Slider("Rim Intensity", rimIntensityProp.floatValue, 0.0f, 1.0f);
                rimPowerProp.floatValue = EditorGUILayout.Slider("Rim Power", rimPowerProp.floatValue, 0.0f, 8.0f);


                //rimColorProp.colorValue = EditorGUILayout.ColorField("Rim Color", rimColorProp.colorValue);
                //Instead do color as a horizontal group with a green label
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rim Color");
                Color newColor = EditorGUILayout.ColorField(rimColorProp.colorValue);
                rimColorProp.colorValue = newColor;
                EditorGUILayout.EndHorizontal();

            }
           
            showEmission = EditorGUILayout.Foldout(showEmission, "Emission", true, foldoutHeaderStyle);
            if (showEmission) {
                materialEditor.TexturePropertySingleLine(new GUIContent("Emissive Mask Texture"), emissiveMaskProp);
                
                
                if (hasMatColor) {
                    EditorGUI.BeginChangeCheck();
                    MaterialColor.ColorSetting settings = matColor.GetColorSettings(materialIndex);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Emissive Color", greenLabelStyle);
                    Color emissiveColor = EditorGUILayout.ColorField(settings.emissiveColor);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Emissive Level", greenLabelStyle);
                    float emissiveLevel = EditorGUILayout.Slider(settings.emissiveLevel, 0.0f, 1.0f);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Emissive Albedo Mix", greenLabelStyle);
                    float albedoMix = EditorGUILayout.Slider(settings.emissiveMix, 0.0f, 1.0f);
                    EditorGUILayout.EndHorizontal();
                                        
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(matColor, "Emissive Changed");
                        settings.emissiveLevel = emissiveLevel;
                        settings.emissiveMix = albedoMix;
                        settings.emissiveColor = emissiveColor;

                        matColor.SetColorSettings(materialIndex, settings);

                    }
                }

            }
        }

        showMetalRoughnessSliders = EditorGUILayout.Foldout(showMetalRoughnessSliders, "Metal/Roughness Sliders", true, foldoutHeaderStyle);
        if (showMetalRoughnessSliders) {

            MRSliderOverrideMixProp.floatValue = EditorGUILayout.Slider("Metal/Roughness Slider Mix",MRSliderOverrideMixProp.floatValue, 0.0f, 1.0f);
            if (MRSliderOverrideMixProp.floatValue > 0) {
                metalOverrideProp.floatValue = EditorGUILayout.Slider("Metal Override", metalOverrideProp.floatValue, 0.0f, 1.0f);
                roughOverrideProp.floatValue = EditorGUILayout.Slider("Roughness Override", roughOverrideProp.floatValue, 0.0f, 1.0f);
            }
}

        // Apply changes
        if (EditorGUI.EndChangeCheck()) {
            foreach (var obj in materialEditor.targets) {
                Material mat = obj as Material;
                if (mat != null) {
                  
                    mat.SetTexture("_MainTex", mainTexProp.textureValue);
                    mat.SetTexture("_NormalTex", normalTexProp.textureValue);
                    mat.SetTexture("_MetalTex", metalTexProp.textureValue);
                    mat.SetTexture("_RoughTex", roughTexProp.textureValue);
                    mat.SetTexture("_EmissiveMaskTex", emissiveMaskProp.textureValue);
                    mat.SetTexture("_ColorMaskTex", colorMaskProp.textureValue);

                    mat.SetFloat("EXTRA_FEATURES", extraFeaturesProp.floatValue);

                    //Set an undo state
                    Undo.RecordObject(mat, "Material Changed");
                    
                }
            }
        }
    }
}
#endif 