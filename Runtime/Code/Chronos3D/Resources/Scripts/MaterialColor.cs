using System;
using System.Collections.Generic;

#if UNITY_EDITOR 
using UnityEditor;
#endif

using UnityEngine;

[ExecuteInEditMode]
public class MaterialColor : MonoBehaviour
{

    [Serializable]
    public class ColorSetting
    {
        public Color materialColor = Color.white;
        public Color emissiveColor = Color.white;
        public float emissiveMix = 1.0f;
        [NonSerialized]
        public MaterialPropertyBlock block;
        public String reference = "";


        public ColorSetting(Color materialColor, Color emissiveColor, float emissiveMix)
        {
            this.materialColor = materialColor;
            this.emissiveColor = emissiveColor;
            this.emissiveMix = emissiveMix;
            block = new MaterialPropertyBlock();
        } 
    }

    [SerializeField]
    public List<ColorSetting> colorSettings = new();
        
    private Renderer ren;
    
    private void Start()
    {
        DoUpdate();
    }

    // Called when the color is changed in the inspector
    private void OnValidate()
    {
        DoUpdate();
    }

    private void OnDestroy()
    {
        if (ren == null) {
            return;
        }
        
        //Destroy all the property blocks
        foreach (var colorSetting in colorSettings)
        {
            for (int i = 0; i < ren.sharedMaterials.Length; i++)
            {
                ren.SetPropertyBlock(null, i);
            }
        }

    }

    public void DoUpdate() {
        RefreshVariables();

        for (int i=0;i<ren.sharedMaterials.Length;i++)
        {

            ColorSetting setting = colorSettings[i];

            var material = ren.sharedMaterials[i];
            setting.reference = material.name;

            if (setting.block == null)
            {
                setting.block = new MaterialPropertyBlock();
            }
            ren.GetPropertyBlock(setting.block, i);

            setting.block.SetColor("_Color", ConvertColor(setting.materialColor));
            setting.block.SetColor("_EmissiveColor", ConvertColor(setting.emissiveColor));
            setting.block.SetFloat("_EmissiveMix", setting.emissiveMix);
            //propertyBlocks[i].SetFloat("EMISSIVE", forceEmissive ? 1 : 0);
            ren.SetPropertyBlock(setting.block, i);
        }
    }

    private void RefreshVariables() {
        //Loop through each material assigned to the renderer on this gameObject
        if (ren == null) {
            ren = GetComponent<Renderer>();
        }
        if (ren == null)
        {
            return;
        }

        //match the colorSettings to materials
        if (colorSettings.Count < ren.sharedMaterials.Length)
        {
            for (int i = colorSettings.Count; i < ren.sharedMaterials.Length; i++)
            {
                colorSettings.Add(new ColorSetting(Color.white, Color.white, 1.0f));
            }
        }
        if (colorSettings.Count > ren.sharedMaterials.Length)
        {
            colorSettings.RemoveRange(ren.sharedMaterials.Length, colorSettings.Count - ren.sharedMaterials.Length);
        }
    }

    private Color ConvertColor(Color color)
    {
        return new Color(Mathf.Pow(color.r, 0.4545454f), Mathf.Pow(color.g, 0.4545454f), Mathf.Pow(color.b, 0.4545454f), color.a);

    }

    public bool SetColor(ColorSetting settings, int materialIndex = 0) {
        RefreshVariables();
        
        if (materialIndex < 0 || materialIndex >= colorSettings.Count) {
            return false;
        }

        colorSettings[materialIndex] = settings;
        
        DoUpdate();
        return true;
    }

    public ColorSetting GetColor(int materialIndex = 0) {
        if (materialIndex < 0 || materialIndex >= colorSettings.Count) {
            return new ColorSetting(Color.white, Color.black, 1);
        }

        return colorSettings[materialIndex];
    }
}

#if UNITY_EDITOR

//Editor for materialColor
[CustomEditor(typeof(MaterialColor))]
public class MaterialColorEditor : Editor
{


    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector();
        //Draw a drawer full of ColorSettings
        int i = 0;
        foreach (MaterialColor.ColorSetting setting in ((MaterialColor)target).colorSettings)
        {
            EditorGUILayout.LabelField("Material Element " + i + " (" + setting.reference + ")");
            setting.materialColor = EditorGUILayout.ColorField("Material Color", setting.materialColor);
            setting.emissiveColor = EditorGUILayout.ColorField("Emissive Color", setting.emissiveColor);
            setting.emissiveMix = EditorGUILayout.Slider("Emissive Mix", setting.emissiveMix, 0.0f, 1.0f);
            //dividing line
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            i++;
        }

        //Call a validate
        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
            ((MaterialColor)target).DoUpdate();
        }


    }
}


#endif