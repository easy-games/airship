using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using Code.Airship.Resources.Scripts.Editor;
using UnityEditor;
#endif

using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[LuauAPI]
[RequireComponent(typeof(Renderer))]
public class MaterialColorURP : MonoBehaviour {

    [Serializable]
    public class ColorSetting {
        public Color baseColor = Color.white;

        [NonSerialized]
        public String reference = "";

        public ColorSetting(Color baseColor) {
            this.baseColor = baseColor;
        }

        public void CopyFrom(ColorSetting otherSettings) {
            this.baseColor = otherSettings.baseColor;

        }
    }

    private const string GeneratedMaterialPath = "Assets/GeneratedColor";
    private const string MaterialColorReferenceFilename = "MaterialColorReferences.asset";

    private static MaterialColorReferences materialColorReferences;

    [SerializeField]
    public List<ColorSetting> colorSettings = new();

    [HideInInspector]
    public bool addedByEditorScript = false;

    [HideInInspector]
    [NonSerialized]
    private List<MaterialPropertyBlock> cachedBlocks = new();

    public string globalIdentifier {
        get {
            // TODO
            // This doesn't work... there is an issue where getting the GlobalObjectId doesn't return in
            // prefab stage view. It is possible that it is an issue with whether the prefab is saved.
            // Anyway, that would be needed to support properly referenced object -> material color
            // TODO
            if (string.IsNullOrEmpty(_globalIdentifier)) {
                // It seems like while in prefab view we can't use GetGlobalObjectIdSlow. But we should be
                // able to get prefab GUID & component id in prefab.
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out string guid, out long localId)) {
                    _globalIdentifier = $"p:{guid}-{localId}";
                } else {
                    var id = GlobalObjectId.GetGlobalObjectIdSlow(this);
                    if (id.identifierType == 0) {
                        Debug.LogError("Unable to generate component reference identifier", this);
                    }
                    _globalIdentifier = id.ToString();
                }
            }

            return _globalIdentifier;
        }
    }
    /// <summary>
    /// Cached because this is slow to access
    /// </summary>
    [NonSerialized] private string _globalIdentifier;

    private Renderer ren;
    public void EditorFirstTimeSetup() {
        for (int i = 0; i < ren.sharedMaterials.Length; i++) {
            ColorSetting setting = colorSettings[i];
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
                        if (this != null) DoUpdate();
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
            for (int i = 0; i < ren.sharedMaterials.Length; i++) {
                ren.SetPropertyBlock(null, i);
            }
        }

    }
    
    public void SetColor(int indx, Color newColor) {
        colorSettings[indx].baseColor = newColor;
        DoUpdate();
    }

    public void CopyFrom(MaterialColorURP other) {
        this.RefreshVariables();
        for (int i = 0; i < other.colorSettings.Count; i++) {
            this.colorSettings[i].baseColor = other.colorSettings[i].baseColor;
        }
        this.DoUpdate();
    }

    public void SetColorOnAll(Color newColor){
        foreach (var colorSetting in colorSettings) {
            colorSetting.baseColor = newColor;
        }
        DoUpdate();
    }

    public ColorSetting GetColorSettingByMaterial(Material mat) {
        for (int i = 0; i < ren.sharedMaterials.Length; i++) {
            if (ren.sharedMaterials[i] == mat) {
                return colorSettings[i];
            }
        }

        return null;

    }

    public void InitializeColorsFromCurrentMaterials() {
        if (this.ren == null) {
            this.ren = GetComponent<Renderer>();
        }

        for (int i = 0; i < ren.sharedMaterials.Length; i++) {
            ColorSetting setting = colorSettings[i];
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
        if (this.ren == null) {
            this.ren = GetComponent<Renderer>();
        }
        
        RefreshVariables();
        SetupMaterialPropertyBlocks();

        for (int i = 0; i < ren.sharedMaterials.Length; i++) {
            Material mat = ren.sharedMaterials[i];
            if (mat == null) continue;
            if (!mat.HasProperty("_BaseColor")) continue;

            ColorSetting setting = colorSettings[i];
            if (mat.GetColor("_BaseColor") == setting.baseColor) continue;

#if UNITY_EDITOR
            if (setting.reference == null || setting.reference == "") {
                setting.reference = mat.name;
            }
#endif

            var usesInstancing = mat.enableInstancing;

            // If this material supports GPU instancing then we color it using MaterialPropertyBlocks.
            // Otherwise we create a new material instance so SRP batching will work (SRP batching breaks with MPBs)
            if (usesInstancing) {
                MaterialPropertyBlock block = cachedBlocks[i];
                ren.GetPropertyBlock(block, i);

                block.SetColor("_BaseColor", (setting.baseColor));

                ren.SetPropertyBlock(block, i);
                continue;
            }

            // var materialInstance = new Material(ren.sharedMaterials[i]);

            // Save asset (and delete old asset)
            Material materialInstance = null;
            var materialName = $"{mat.shader.name.Replace("/", "_")} ({setting.baseColor})";
            if (!Application.isPlaying) {
#if UNITY_EDITOR
                // During edit time check for cached material in generated materials folder
                CheckGeneratedFolderSetup();
                
                var previousMaterial = ren.sharedMaterials[i];
                var assetPath =
                    $"{GeneratedMaterialPath}/{materialName}.mat";
                if ((materialInstance = AssetDatabase.LoadAssetAtPath<Material>(assetPath)) == null) {
                    materialInstance = new Material(ren.sharedMaterials[i]);
                    materialInstance.name = $"{materialName}";
                    materialInstance.SetColor("_BaseColor", setting.baseColor);
                    
                    AssetDatabase.CreateAsset(materialInstance, assetPath);
                    AssetDatabase.SaveAssets();
                }
                materialColorReferences.Reference(materialInstance, globalIdentifier);
                
                // Dereference after creating material copy
                var previousMaterialPath = AssetDatabase.GetAssetPath(previousMaterial);
                if (previousMaterialPath.Contains(GeneratedMaterialPath)) {
                    ren.sharedMaterials[i] = null;
                    materialColorReferences.Dereference(previousMaterial, globalIdentifier);
                }
#endif
            } else {
                // At runtime always generated a new material if needed
                materialInstance = new Material(ren.sharedMaterials[i]);
                materialInstance.name = $"{materialName}";
                materialInstance.SetColor("_BaseColor", setting.baseColor);
            }
            
            var materials = ren.sharedMaterials;
            materials[i] = materialInstance;
            ren.sharedMaterials = materials; 
            
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                // Mark renderer as dirty to save instanced material
                EditorUtility.SetDirty(ren);
            }
#endif
        }
    }

    private void CheckGeneratedFolderSetup() {
#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(GeneratedMaterialPath)) {
            AssetDatabase.CreateFolder("Assets", GeneratedMaterialPath.Split("Assets/")[1]);
        }

        var referencePath = $"{GeneratedMaterialPath}/{MaterialColorReferenceFilename}";
        if (!(materialColorReferences = AssetDatabase.LoadAssetAtPath<MaterialColorReferences>(referencePath))) {
            materialColorReferences = ScriptableObject.CreateInstance<MaterialColorReferences>();
            AssetDatabase.CreateAsset(materialColorReferences, referencePath);
            AssetDatabase.SaveAssetIfDirty(materialColorReferences);
        }
#endif
    }

    private void SetupMaterialPropertyBlocks() {
        //Make sure cachedBlocks is the same size as ren.shadredMAterials
        while (cachedBlocks.Count < ren.sharedMaterials.Length) {
            cachedBlocks.Add(new MaterialPropertyBlock());
        }
        //Also shrink it
        while (cachedBlocks.Count > ren.sharedMaterials.Length) {
            cachedBlocks.RemoveAt(cachedBlocks.Count - 1);
        }

        for (int i = 0; i < ren.sharedMaterials.Length; i++) {
            ren.SetPropertyBlock(null, i);
        }
    }

    [HideFromTS]
    public void RefreshVariables() {
        // Loop through each material assigned to the renderer on this gameObject
        // match the colorSettings to materials
        if (colorSettings.Count < ren.sharedMaterials.Length) {
            for (int i = colorSettings.Count; i < ren.sharedMaterials.Length; i++) {
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
public class MaterialColorURPEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
        if (targets.Length == 1) {
            //single object
            //Draw a drawer full of ColorSettings
            MaterialColorURP targetObj = (MaterialColorURP)targets[0];
            Undo.RecordObject(targetObj, "Edit Material Color");

            int i = 0;
            foreach (MaterialColorURP.ColorSetting setting in ((MaterialColorURP)targetObj).colorSettings) {
                EditorGUILayout.LabelField("Material Element " + i + " (" + setting.reference + ")");

                //Gamma Color Picker
                setting.baseColor = EditorGUILayout.ColorField(new GUIContent("Base Color"), setting.baseColor);


                //dividing line
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                i++;
            }
            //Call a validate
            if (GUI.changed) {
                ((MaterialColorURP)targetObj).DoUpdate();
                EditorUtility.SetDirty(targetObj);
            }
        }

        if (targets.Length > 1) {
            Undo.RecordObject(target, "Edit Material Color");

            int max = 0;
            foreach (MaterialColorURP targetObj in targets) {
                Undo.RecordObject(targetObj, "Edit Material Color");
                if (targetObj.colorSettings.Count > max) {
                    max = targetObj.colorSettings.Count;
                }
            }

            List<MaterialColorURP.ColorSetting> originalValues = new List<MaterialColorURP.ColorSetting>();

            for (int i = 0; i < max; i++) {
                bool first = true;

                int numItems = 0;
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
                        foreach (string name in names) {
                            EditorGUILayout.LabelField(name);
                        }

                        first = false;

                        MaterialColorURP.ColorSetting setting = targetObj.colorSettings[i];

                        setting.baseColor = EditorGUILayout.ColorField("Base Color", setting.baseColor);

                        //dividing line
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                    }
                }
            }
            if (GUI.changed) {
                for (int i = 0; i < max; i++) {
                    MaterialColorURP hostObject = null;
                    foreach (MaterialColorURP targetObj in targets) {
                        if (targetObj.colorSettings.Count <= i) {
                            continue;
                        }

                        if (hostObject == null) {
                            hostObject = targetObj;

                            //Compare to the original value, if its the same as it was, we break out and dont set any of the others
                            MaterialColorURP.ColorSetting originalValue = originalValues[i];
                            MaterialColorURP.ColorSetting newValue = targetObj.colorSettings[i];

                            if (originalValue.baseColor == newValue.baseColor) {
                                break;
                            }
                        }
                        else {
                            MaterialColorURP.ColorSetting setting = targetObj.colorSettings[i];
                            MaterialColorURP.ColorSetting hostSetting = hostObject.colorSettings[i];

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