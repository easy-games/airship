using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;

public class HierarchyMaterialConverter {
    [MenuItem("GameObject/Airship/Convert To Airship Materials", false, 2510)]
    static void ConvertToAirshipMaterialsCommand(MenuCommand command) {
        GameObject selectedGameObject = Selection.activeGameObject;
        if (selectedGameObject == null) {
            Debug.LogWarning("No GameObject selected.");
            return;
        }

        ConvertToAirshipMaterials(selectedGameObject);
    }

    public static void ConvertToAirshipMaterials(GameObject selectedGameObject) {

        Renderer[] renderers = selectedGameObject.GetComponentsInChildren<Renderer>();
        // Only build a replacement material once for each existing material
        Dictionary<Material, Material> usedReplacements = new();
        foreach (Renderer rend in renderers) {
            Material[] materials = rend.sharedMaterials;

            for (int i = 0; i < materials.Length; i++) {
                Material mat = materials[i];
                // Check if this material needs conversion
                if (NeedsConversion(mat) == true) {
                    if (!usedReplacements.TryGetValue(mat, out var newAirshipMaterial)) {
                        newAirshipMaterial = CreateOrGetAirshipMaterial(mat, rend.gameObject);
                    }
                    usedReplacements.TryAdd(mat, newAirshipMaterial);
                    materials[i] = newAirshipMaterial;
                }
            }

            rend.sharedMaterials = materials;
            //UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rend);

        }

        Debug.LogWarning($"Converted materials for '{selectedGameObject.name}' to Airship materials.");
    }

    // Validate the menu item defined by the function above
    [MenuItem("GameObject/Airship/Convert To Airship Materials", true)]
    static bool ValidateConvertToAirshipMaterials(MenuCommand command) {
        // This function decides whether the menu item is enabled or not
        // For now, it's always enabled if any GameObject is selected
        return Selection.activeGameObject != null;
    }
        
    static Material CreateOrGetAirshipMaterial(Material baseMaterial, GameObject debugObjectName) {

        //Find a unity  asset named the same as this baseMaterial
        string path = FindMaterialPath(baseMaterial);
        
        //if we found it, see if theres the _Airship version there too
        if (path != null && path != "") {
            //Debug.Log("Found at path: " + path);
            string existingConversionPath = MakeAirshipMaterialName(path);
            
            Material airshipMaterial = AssetDatabase.LoadAssetAtPath<Material>(existingConversionPath);
            if (airshipMaterial != null) {
                Debug.Log("Swapping material on " + debugObjectName.name + " " + baseMaterial.name + " for existing " + existingConversionPath);
                return airshipMaterial;
            }
        }

        //Else create it and save it

        //See what shader we should be using - most of the time its just AirshipWorldShader but particles might be using some of the mobile shaders
        bool setBlendMode = false;
        UnityEngine.Rendering.BlendMode srcBlend = 0;
        UnityEngine.Rendering.BlendMode dstBlend = 0;

        string shaderName = "@Easy/CoreMaterials/Shared/Resources/BaseShaders/AirshipWorldShaderPBR.shader";
        switch (baseMaterial.shader.name) {
            case "Mobile/Particles/Alpha Blended":
                shaderName = "@Easy/CoreMaterials/Shared/Resources/BaseShaders/SpriteShaders/AirshipSpriteAlphaBlend.shader";
                setBlendMode = true;
                srcBlend = UnityEngine.Rendering.BlendMode.SrcAlpha;
                dstBlend = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

            break;
            case "Mobile/Particles/Additive":
                shaderName = "@Easy/CoreMaterials/Shared/Resources/BaseShaders/SpriteShaders/AirshipSpriteAlphaBlend.shader";
                setBlendMode = true;
                srcBlend = UnityEngine.Rendering.BlendMode.SrcAlpha;
                dstBlend = UnityEngine.Rendering.BlendMode.One;
            
            break;
            case "Mobile/Particles/Multiply":
                shaderName = "@Easy/CoreMaterials/Shared/Resources/BaseShaders/SpriteShaders/AirshipSpriteAlphaBlend.shader";
                setBlendMode = true;
                srcBlend = UnityEngine.Rendering.BlendMode.DstColor;
                dstBlend = UnityEngine.Rendering.BlendMode.Zero;
            break;
        }
        
        Shader shader = AssetBridge.Instance.LoadAssetInternal<Shader>(shaderName);
        
        if (shader == null) {
            Debug.LogError(shaderName + " not found - cancelled material conversion");
            return null;
        }
        Material newMaterial = new Material(shader);
        newMaterial.name = baseMaterial.name + "_Airship";

        CopyPropertiesToAirshipMaterial(baseMaterial, newMaterial);

        if (setBlendMode) {
            newMaterial.SetInt("_SrcBlend", (int)srcBlend);
            newMaterial.SetInt("_DstBlend", (int)dstBlend);
        }
        
        if (path != null && path != "") {
            string finalPath = MakeAirshipMaterialName(path);
            AssetDatabase.CreateAsset(newMaterial, finalPath);
            Debug.Log("Swapping material on " + debugObjectName.name + " " + baseMaterial.name + " for new material at " + finalPath);
        }

        return newMaterial;
    }

    public static string MakeAirshipMaterialName(string path) {
        //Convert paths like  "resources/filename" and "resources/filename.mat" to
        //                    "resources/filename_Airship" and "resources/filename_Airship.mat"

        string finalPath = path;
        bool hasMat = false;
        if (path.ToLower().EndsWith(".mat")) {
            hasMat = true;
            finalPath = path.Substring(0, path.Length - 4);
        }
        finalPath += "_Airship";
        if (hasMat) {
            finalPath += ".mat";
        }
        return finalPath;
    }

    public static string FindMaterialPath(Material material) {
        string path = AssetDatabase.GetAssetPath(material);

        if (path.Contains("unity_builtin")) {
            return null;
        }

        return path;
    }

    public static void CopyPropertiesToAirshipMaterial(Material baseMaterial, Material airshipMaterial) {

        if (baseMaterial.HasProperty("_MainTex")) {
            airshipMaterial.SetTexture("_MainTex", baseMaterial.GetTexture("_MainTex"));
        }
        else if (baseMaterial.HasProperty("_BaseColorMap")) {
            airshipMaterial.SetTexture("_MainTex", baseMaterial.GetTexture("_BaseColorMap"));
        }

        //_Color
        if (baseMaterial.HasProperty("_Color")) {
            airshipMaterial.SetColor("_Color", baseMaterial.GetColor("_Color"));
        }
        else if (baseMaterial.HasProperty("_BaseColor")) {
            airshipMaterial.SetColor("_Color", baseMaterial.GetColor("_BaseColor"));
        }

        //Normal map
        if (baseMaterial.HasProperty("_BumpMap")) {
            airshipMaterial.SetTexture("_NormalTex", baseMaterial.GetTexture("_BumpMap"));
        }
        else if (baseMaterial.HasProperty("_NormalMap")) {
            airshipMaterial.SetTexture("_NormalTex", baseMaterial.GetTexture("_NormalMap"));
        }


        //Fix map settings
        Texture diffuse = airshipMaterial.GetTexture("_MainTex");
        if (diffuse) {
            //Here's the diffuse map, grab its asset and modify its srgb setting
            string path = AssetDatabase.GetAssetPath(diffuse);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null) {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
                Debug.Log("Updated texture settings for " + path);
            }
            //else {
            //    Debug.LogError("Failed to get texture importer for " + path);
            //}

        }
    }

    public static bool NeedsConversion(GameObject obj) {
        //Get a list of all materials in it
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers) {
            //See if these shaders have AirshipPipeline Tags
            Material[] materials = rend.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) {
                Material mat = materials[i];

                if (mat == null) {
                    continue;
                }

                if (NeedsConversion(mat) == true) {
                    return true;
                }


            }

        }
        return false;
    }

    static UnityEngine.Rendering.ShaderTagId lightMode = new("LightMode");
    static UnityEngine.Rendering.ShaderTagId airshipForwardPass = new("AirshipForwardPass");
    static UnityEngine.Rendering.ShaderTagId airshipShadowPass = new("AirshipShadowPass");
    public static bool NeedsConversion(Material mat) {
        if (mat == null) {
            return false;
        }

        Shader shader = mat.shader;

        for (int i = 0; i < shader.passCount; i++) {
            var mode = shader.FindPassTagValue(i, lightMode);
            if (mode == airshipForwardPass || mode == airshipShadowPass) {
                return false;
            }
        }

        //didnt find any exceptions
        return true;
    }


    static UnityEngine.Rendering.ShaderTagId pipeline = new("Pipeline");
    static UnityEngine.Rendering.ShaderTagId airship = new("Airship");
    
    public static bool IsAirshipPipeline(Material mat) {
        if (mat == null) {
            return false;
        }

        Shader shader = mat.shader;

        for (int i = 0; i < shader.passCount; i++) {
            var mode = shader.FindPassTagValue(i, pipeline);
            if (mode == airship) {
                return true;
            }
        }
        return false;
    }
}


[InitializeOnLoad]
public static class HierarchyChangedDetector {
    static List<GameObject> lastHierarchyRootObjects;
    static double lastTime = 0;

    static HierarchyChangedDetector() {

        // Subscribe to the hierarchyChanged event
        EditorApplication.hierarchyChanged += OnHierarchyChanged;

        EditorApplication.update += Update;

        // Initialize with current hierarchy root objects
        UpdateHierarchySnapshot();
    }

    
    //update
    static void Update() {
#if AIRSHIP_PLAYER
        return;
#endif

        //Make sure 1 second has passed
        if (EditorApplication.timeSinceStartup - lastTime < 1) {
            return;
        }
        lastTime = EditorApplication.timeSinceStartup;
        
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        
        if (EditorIntegrationsConfig.instance.autoConvertMaterials == false) {
            return;
        }
        
        UpdateSkyboxMaterials();
    }
    

    private static void OnHierarchyChanged() {
        //Check to make sure we're not playing
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }

        if (EditorIntegrationsConfig.instance.autoConvertMaterials == false) {
            return;
        }

        var currentHierarchyRootObjects = GetRootHierarchyObjects();
        HashSet<GameObject> newRoots = new HashSet<GameObject>();

        foreach (var obj in currentHierarchyRootObjects) {
            if (!lastHierarchyRootObjects.Contains(obj)) {
                // Get the root of this object if it's part of a new hierarchy
                var root = GetRootParent(obj);
                newRoots.Add(root);
            }
        }

        // Log new root objects
        foreach (var newRoot in newRoots) {
            if (HierarchyMaterialConverter.NeedsConversion(newRoot)) {
                HierarchyMaterialConverter.ConvertToAirshipMaterials(newRoot);
            }
        }

        // Update the snapshot for next comparison
        UpdateHierarchySnapshot();
    }

    private static GameObject[] GetRootHierarchyObjects() {
        // Get all GameObjects in the scene, but not filtering to root objects yet
        return Object.FindObjectsOfType<GameObject>();
    }

    private static void UpdateHierarchySnapshot() {
        lastHierarchyRootObjects = new List<GameObject>(GetRootHierarchyObjects());
    }

    private static GameObject GetRootParent(GameObject obj) {
        // Find the highest-level parent of the given object
        Transform currentParent = obj.transform.parent;
        while (currentParent != null) {
            if (currentParent.parent == null)
                break; // This is the highest-level parent
            currentParent = currentParent.parent;
        }

        return currentParent != null ? currentParent.gameObject : obj; // Return the root parent, or the object itself if no parent
    }

    private static void UpdateSkyboxMaterials() {

        Material skyMaterial = RenderSettings.skybox;

        if (skyMaterial == null) {
            return;
        }

        if (HierarchyMaterialConverter.IsAirshipPipeline(skyMaterial)) {
            return;
        }

        //Needs conversion
        //Switch the shader to  
        var shader =
         AssetBridge.Instance.LoadAssetInternal<Shader>(
             "@Easy/CoreMaterials/Shared/Resources/BaseShaders/SkyboxShader.shader", false);
                
        if (shader == null) {
            //Debug.LogError("SkyboxShader not found for conversion");
            return;
        }

        //Grab the cubemap
        if (skyMaterial.HasProperty("_Tex")) {
            Texture texture = skyMaterial.GetTexture("_Tex");

            skyMaterial.shader = shader;

            //Copy the map over
            if (texture) {
                skyMaterial.SetTexture("_CubemapTex", texture);
            }
        }
               
        //Grab the original asset
        string path = AssetDatabase.GetAssetPath(skyMaterial);
        if (path != null) {
            // Debug.Log("Converted rendersettings skybox material to Airship pipeline skybox at path: " + path);
        }
        else {
            Debug.Log("Converted rendersettings skybox material to Airship pipeline skybox (no path?)");
        }
    }
}

#endif