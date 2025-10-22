using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

[ExecuteInEditMode]
[LuauAPI]
[RequireComponent(typeof(Renderer))]
public class MaterialColorURP : MonoBehaviour {
    [Serializable]
    public class ColorSetting {
        public Color baseColor = Color.white;

        [NonSerialized]
        public string reference = "";

        public ColorSetting(Color baseColor) {
            this.baseColor = baseColor;
        }

        public void CopyFrom(ColorSetting otherSettings) {
            baseColor = otherSettings.baseColor;
        }
    }

    [SerializeField]
    public List<ColorSetting> colorSettings = new();

    [HideInInspector]
    public bool addedByEditorScript = false;

    [HideInInspector]
    [NonSerialized]
    private List<MaterialPropertyBlock> cachedBlocks = new();

    private Renderer ren;

    public void EditorFirstTimeSetup() {
        for (var i = 0; i < ren.sharedMaterials.Length; i++) {
            var setting = colorSettings[i];
            var material = ren.sharedMaterials[i];
            if (material == null) {
                continue;
            }

            if (material.HasProperty("_BaseColor")) {
                var startingColor = material.GetColor("_BaseColor");
                setting.baseColor = startingColor;
            }
        }
    }

    // Called when the color is changed in the inspector
    private void OnValidate() {
        if (!enabled) {
            return;
        }
#if UNITY_EDITOR
        if (Application.isPlaying) {
            DoUpdate();
        } else {
            EditorApplication.delayCall += () => {
                if (this != null) {
                    DoUpdate();
                }
            };
        }
#else
        DoUpdate();
#endif
    }

    private void OnEnable() {
        DoUpdate();
    }

    private void OnDisable() {
        if (ren == null) {
            return;
        }

        //Destroy all the property blocks
        foreach (var colorSetting in colorSettings) {
            for (var i = 0; i < ren.sharedMaterials.Length; i++) {
                ren.SetPropertyBlock(null, i);
            }
        }
    }

    public void SetColor(int index, Color newColor) {
        if (index < 0 || index >= colorSettings.Count) {
            return;
        }

        colorSettings[index].baseColor = newColor;
        DoUpdate();
    }

    public void CopyFrom(MaterialColorURP other) {
        RefreshVariables();
        for (var i = 0; i < other.colorSettings.Count; i++) {
            colorSettings[i].baseColor = other.colorSettings[i].baseColor;
        }

        DoUpdate();
    }

    public void SetColorOnAll(Color newColor) {
        foreach (var colorSetting in colorSettings) {
            colorSetting.baseColor = newColor;
        }

        DoUpdate();
    }

    public ColorSetting GetColorSettingByMaterial(Material mat) {
        for (var i = 0; i < ren.sharedMaterials.Length; i++) {
            if (ren.sharedMaterials[i] == mat) {
                return colorSettings[i];
            }
        }

        return null;
    }

    public void InitializeColorsFromCurrentMaterials() {
        if (ren == null) {
            ren = GetComponent<Renderer>();
        }

        for (var i = 0; i < ren.sharedMaterials.Length; i++) {
            var setting = colorSettings[i];
            var material = ren.sharedMaterials[i];
            if (material == null) {
                continue;
            }

            if (material.HasProperty("_BaseColor")) {
                var startingColor = material.GetColor("_BaseColor");
                setting.baseColor = startingColor;
            }
        }
    }


    public void DoUpdate() {
        if (ren == null) {
            ren = GetComponent<Renderer>();
        }

        RefreshVariables();

        //Make sure cachedBlocks is the same size as ren.shadredMAterials
        while (cachedBlocks.Count < ren.sharedMaterials.Length) {
            cachedBlocks.Add(new MaterialPropertyBlock());
        }

        //Also shrink it
        while (cachedBlocks.Count > ren.sharedMaterials.Length) {
            cachedBlocks.RemoveAt(cachedBlocks.Count - 1);
        }

        for (var i = 0; i < ren.sharedMaterials.Length; i++) {
            ren.SetPropertyBlock(null, i);
        }

        for (var i = 0; i < ren.sharedMaterials.Length; i++) {
            var mat = ren.sharedMaterials[i];
            if (mat == null) {
                continue;
            }

            var setting = colorSettings[i];

#if UNITY_EDITOR
            if (setting.reference == null || setting.reference == "") {
                setting.reference = mat.name;
            }
#endif

            var block = cachedBlocks[i];
            ren.GetPropertyBlock(block, i);

            block.SetColor("_BaseColor", setting.baseColor);

            ren.SetPropertyBlock(block, i);
        }
    }

    [HideFromTS]
    public void RefreshVariables() {
        // Loop through each material assigned to the renderer on this gameObject
        // match the colorSettings to materials
        if (colorSettings.Count < ren.sharedMaterials.Length) {
            for (var i = colorSettings.Count; i < ren.sharedMaterials.Length; i++) {
                colorSettings.Add(new ColorSetting(Color.white));
            }
        }

        if (colorSettings.Count > ren.sharedMaterials.Length) {
            colorSettings.RemoveRange(ren.sharedMaterials.Length, colorSettings.Count - ren.sharedMaterials.Length);
        }
    }


    public void Clear() {
        colorSettings.Clear();
        cachedBlocks.Clear();
    }
}

#if UNITY_EDITOR
// Editor for MaterialColor
[CustomEditor(typeof(MaterialColorURP))]
[CanEditMultipleObjects]
public class MaterialColorURPEditor : Editor {
    public override void OnInspectorGUI() {
        if (targets.Length == 1) {
            //single object
            //Draw a drawer full of ColorSettings
            var targetObj = (MaterialColorURP)targets[0];
            Undo.RecordObject(targetObj, "Edit Material Color");

            var i = 0;
            foreach (var setting in ((MaterialColorURP)targetObj).colorSettings) {
                EditorGUILayout.LabelField("Material Element " + i + " (" + setting.reference + ")");

                //Gamma Color Picker
                setting.baseColor = EditorGUILayout.ColorField(new GUIContent("Base Color"), setting.baseColor);


                //dividing line
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                i++;
            }

            //Call a validate
            if (GUI.changed) {
                EditorUtility.SetDirty(targetObj);
                ((MaterialColorURP)targetObj).DoUpdate();
            }
        }

        if (targets.Length > 1) {
            Undo.RecordObject(target, "Edit Material Color");

            var max = 0;
            foreach (MaterialColorURP targetObj in targets) {
                Undo.RecordObject(targetObj, "Edit Material Color");
                if (targetObj.colorSettings.Count > max) {
                    max = targetObj.colorSettings.Count;
                }
            }

            var originalValues = new List<MaterialColorURP.ColorSetting>();

            for (var i = 0; i < max; i++) {
                var first = true;

                var numItems = 0;
                List<string> names = new();


                foreach (MaterialColorURP targetObj in targets) {
                    if (targetObj.colorSettings.Count <= i) {
                        continue;
                    }

                    numItems += 1;
                    names.Add(targetObj.gameObject.name);
                }


                foreach (MaterialColorURP targetObj in targets) {
                    if (targetObj.colorSettings.Count <= i) {
                        continue;
                    }

                    //Display the first one
                    if (first == true) {
                        //Add a clone
                        originalValues.Add(new MaterialColorURP.ColorSetting(targetObj.colorSettings[i].baseColor));

                        EditorGUILayout.LabelField("Multiple Objects (" + numItems + ") at index " + i);
                        //Display all the names in a list
                        foreach (var name in names) {
                            EditorGUILayout.LabelField(name);
                        }

                        first = false;

                        var setting = targetObj.colorSettings[i];

                        setting.baseColor = EditorGUILayout.ColorField("Base Color", setting.baseColor);

                        //dividing line
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    }
                }
            }

            if (GUI.changed) {
                for (var i = 0; i < max; i++) {
                    MaterialColorURP hostObject = null;
                    foreach (MaterialColorURP targetObj in targets) {
                        if (targetObj.colorSettings.Count <= i) {
                            continue;
                        }

                        if (hostObject == null) {
                            hostObject = targetObj;

                            //Compare to the original value, if its the same as it was, we break out and dont set any of the others
                            var originalValue = originalValues[i];
                            var newValue = targetObj.colorSettings[i];

                            if (originalValue.baseColor == newValue.baseColor) {
                                break;
                            }
                        } else {
                            var setting = targetObj.colorSettings[i];
                            var hostSetting = hostObject.colorSettings[i];

                            setting.baseColor = hostSetting.baseColor;
                        }
                    }
                }

                foreach (MaterialColorURP targetObj in targets) {
                    EditorUtility.SetDirty(targetObj);
                    targetObj.DoUpdate();
                }
            }
        }
    }
}
#endif