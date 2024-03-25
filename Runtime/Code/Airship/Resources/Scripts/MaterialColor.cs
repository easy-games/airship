using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

[ExecuteInEditMode]
[LuauAPI]
public class MaterialColor : MonoBehaviour
{

    [Serializable]
    public class ColorSetting
    {
        public Color materialColor = Color.white;
        public Color emissiveColor = Color.white;
        public float emissiveMix = 1.0f;
        [NonSerialized]
        public String reference = "";


        public ColorSetting(Color materialColor, Color emissiveColor, float emissiveMix)
        {
            this.materialColor = materialColor;
            this.emissiveColor = emissiveColor;
            this.emissiveMix = emissiveMix;
        }

        public void CopyFrom(ColorSetting otherSettings)
        {
            this.materialColor = otherSettings.materialColor;
            this.emissiveColor = otherSettings.emissiveColor;
            this.emissiveMix = otherSettings.emissiveMix;
        }
    }

    [SerializeField]
    public List<ColorSetting> colorSettings = new();

    [HideInInspector]
    public bool addedByEditorScript = false;

    private Renderer ren;

    public void EditorFirstTimeSetup() 
    {
        for (int i = 0; i < ren.sharedMaterials.Length; i++) 
        {
            ColorSetting setting = colorSettings[i];
            var material = ren.sharedMaterials[i];
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_Color")) 
            {
                var startingColor = material.GetColor("_Color");
                setting.materialColor = startingColor;
            }
        }
    }

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
        if (ren == null)
        {
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

    public void DoUpdate()
    {
        RefreshVariables();

        if (ren == null)
        {
            return;
        }

        var rendererReference = AirshipRendererManager.Instance.GetRendererReference(ren);
        
        for (int i = 0; i < ren.sharedMaterials.Length; i++)
        {
            Material mat = ren.sharedMaterials[i];
            if (mat == null)
            {
                continue;
            }
            
            ColorSetting setting = colorSettings[i];

#if UNITY_EDITOR
            if (setting.reference == null || setting.reference == "")
            {
                setting.reference = mat.name;
            }
#endif            

            MaterialPropertyBlock block = rendererReference.GetPropertyBlock(mat, i);
            
            Debug.Log(gameObject.GetInstanceID() + " Converted Color: " + setting.materialColor.linear);
            block.SetColor("_Color", setting.materialColor);
            block.SetColor("_EmissiveColor", ConvertColor(setting.emissiveColor));
            block.SetFloat("_EmissiveMix", setting.emissiveMix);
        }
    }

    private void RefreshVariables()
    {
        //Loop through each material assigned to the renderer on this gameObject
        if (ren == null)
        {
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

    public Color ConvertColor(Color color)
    {
        return new Color(Mathf.Pow(color.r, 0.4545454f), Mathf.Pow(color.g, 0.4545454f), Mathf.Pow(color.b, 0.4545454f), color.a);

    }

    public void SetAllColors(Color diffuseColor, bool multiplyColor = false)
    {
        foreach (var setting in colorSettings)
        {
            if (multiplyColor)
            {
                setting.materialColor *= diffuseColor;
            }
            else
            {
                setting.materialColor = diffuseColor;
            }
        }
        DoUpdate();
    }

    public void SetAllEmissive(Color emissiveColor, float emissiveMix) {
        foreach (var setting in colorSettings)
        {
            setting.emissiveColor = emissiveColor;
            setting.emissiveMix = emissiveMix;
        }
        DoUpdate();
    }

    public bool SetColor(ColorSetting settings, int materialIndex = 0)
    {
        RefreshVariables();

        if (materialIndex < 0 || materialIndex >= colorSettings.Count)
        {
            return false;
        }

        colorSettings[materialIndex].CopyFrom(settings);

        DoUpdate();
        return true;
    }

    public void SetMaterialColor(int index, Color color)
    {
        if (index < this.colorSettings.Count)
        {
            colorSettings[index].materialColor = color;
            DoUpdate();
        }
    }

    public ColorSetting GetColor(int materialIndex = 0)
    {
        if (materialIndex < 0 || materialIndex >= colorSettings.Count)
        {
            return new ColorSetting(Color.white, Color.black, 1);
        }

        return colorSettings[materialIndex];
    }
}

#if UNITY_EDITOR

// Editor for MaterialColor
[CustomEditor(typeof(MaterialColor))]
[CanEditMultipleObjects]
public class MaterialColorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (targets.Length == 1)
        {
            //single object
            //Draw a drawer full of ColorSettings
            MaterialColor targetObj = (MaterialColor)targets[0];
            Undo.RecordObject(targetObj, "Edit Material Color");
            
            int i = 0;
            foreach (MaterialColor.ColorSetting setting in ((MaterialColor)targetObj).colorSettings)
            {
                EditorGUILayout.LabelField("Material Element " + i + " (" + setting.reference + ")");
                
                //Linear Color Picker
                // setting.materialColor = EditorGUILayout.ColorField(new GUIContent("Material Color"), setting.materialColor, true, true, true);
                // setting.emissiveColor = EditorGUILayout.ColorField(new GUIContent("Emissive Color"), setting.emissiveColor, true, true, true);

                //Gamma Color Picker
                setting.materialColor = EditorGUILayout.ColorField(new GUIContent("Material Color"), setting.materialColor);
                setting.emissiveColor = EditorGUILayout.ColorField(new GUIContent("Emissive Color"), setting.emissiveColor);

                setting.emissiveMix = EditorGUILayout.Slider("Emissive Mix", setting.emissiveMix, 0.0f, 1.0f);
                //dividing line
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                i++;
            }
            //Call a validate
            if (GUI.changed)
            {
                EditorUtility.SetDirty(targetObj);
                ((MaterialColor)targetObj).DoUpdate();
            }
        }

        if (targets.Length > 1)
        {
            Undo.RecordObject(target, "Edit Material Color");

            int max = 0;
            foreach (MaterialColor targetObj in targets)
            {
                Undo.RecordObject(targetObj, "Edit Material Color");
                if (targetObj.colorSettings.Count > max)
                {
                    max = targetObj.colorSettings.Count;
                }
            }

            List<MaterialColor.ColorSetting> originalValues = new List<MaterialColor.ColorSetting>();

            for (int i = 0; i < max; i++)
            {
                bool first = true;

                int numItems = 0;
                List<string> names = new();


                foreach (MaterialColor targetObj in targets)
                {
                    if (targetObj.colorSettings.Count <= i)
                    {
                        continue;
                    }
                    numItems += 1;
                    names.Add(targetObj.gameObject.name);
                }


                foreach (MaterialColor targetObj in targets)
                {
                    if (targetObj.colorSettings.Count <= i)
                    {
                        continue;
                    }

                    //Display the first one
                    if (first == true)
                    {
                        //Add a clone
                        originalValues.Add(new MaterialColor.ColorSetting(targetObj.colorSettings[i].materialColor, targetObj.colorSettings[i].emissiveColor, targetObj.colorSettings[i].emissiveMix));

                        EditorGUILayout.LabelField("Multiple Objects (" + numItems + ") at index " + i);
                        //Display all the names in a list
                        foreach (string name in names)
                        {
                            EditorGUILayout.LabelField(name);
                        }

                        first = false;

                        MaterialColor.ColorSetting setting = targetObj.colorSettings[i];

                        setting.materialColor = EditorGUILayout.ColorField("Material Color", setting.materialColor);
                        setting.emissiveColor = EditorGUILayout.ColorField("Emissive Color", setting.emissiveColor);
                        setting.emissiveMix = EditorGUILayout.Slider("Emissive Mix", setting.emissiveMix, 0.0f, 1.0f);
                        //dividing line
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                    }
                }
            }
            if (GUI.changed)
            {
                for (int i = 0; i < max; i++)
                {
                    MaterialColor hostObject = null;
                    foreach (MaterialColor targetObj in targets)
                    {
                        if (targetObj.colorSettings.Count <= i)
                        {
                            continue;
                        }

                        if (hostObject == null)
                        {
                            hostObject = targetObj;

                            //Compare to the original value, if its the same as it was, we break out and dont set any of the others
                            MaterialColor.ColorSetting originalValue = originalValues[i];
                            MaterialColor.ColorSetting newValue = targetObj.colorSettings[i];

                            if (originalValue.materialColor == newValue.materialColor && originalValue.emissiveColor == newValue.emissiveColor && originalValue.emissiveMix == newValue.emissiveMix)
                            {
                                break;
                            }
                        }
                        else
                        {
                            MaterialColor.ColorSetting setting = targetObj.colorSettings[i];
                            MaterialColor.ColorSetting hostSetting = hostObject.colorSettings[i];

                            setting.materialColor = hostSetting.materialColor;
                            setting.emissiveColor = hostSetting.emissiveColor;
                            setting.emissiveMix = hostSetting.emissiveMix;
                        }
                    }
                }

                foreach (MaterialColor targetObj in targets)
                {

                    EditorUtility.SetDirty(targetObj);
                    targetObj.DoUpdate();
                }
            }
        }
    }
}
#endif