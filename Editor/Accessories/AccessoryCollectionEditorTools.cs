using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System;
using Code.Player.Accessories;

public class AccessoryCollectionTools {
    private const SelectionMode AssetModeMask = SelectionMode.Assets | SelectionMode.TopLevel | SelectionMode.DeepAssets;
    private static List<string> processedPaths = new List<string>();
    private static Material defaultMat; 

    [MenuItem("Airship/Avatar/Fill All Avatar Accessories")]
    private static void FillAvatarCollection() {
        Debug.Log("Grabbing all avatar accessories");
        string folderPath = Application.dataPath + "/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems";
        string allItemsPath
            = "Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset";
        
        AvatarAccessoryCollection allAccessories = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(allItemsPath);

        //Compile accessories
        List<AccessoryComponent> accs = new List<AccessoryComponent>();
        int count = 0;
        GetAccessoriesInFolder(ref count, folderPath, "prefab", (relativePath)=>{
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                if(go != null){
                    var acc = go.GetComponent<AccessoryComponent>();
                    if (acc != null) {
                        //Debug.Log("Found Accessory: " + relativePath);
                        accs.Add(acc);
                    }
                }
        });
        allAccessories.accessories = accs.ToArray();

        //Compile faces
        List<AccessoryFace> faces = new List<AccessoryFace>();
        count = 0;
        GetAccessoriesInFolder(ref count, folderPath, "asset", (relativePath)=>{
                var face = AssetDatabase.LoadAssetAtPath<AccessoryFace>(relativePath);
                if (face != null) {
                    //Debug.Log("Found Face: " + relativePath);
                    faces.Add(face);
                }
        });
        allAccessories.faces = faces.ToArray();

        //Save
        EditorUtility.SetDirty(allAccessories);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void GetAccessoriesInFolder(ref int count, string folderPath, string filetype, Action<string> packCallback) {
        if (!Directory.Exists(folderPath)) {
            Debug.LogWarning("No folder found: " + folderPath);
            return;
        }

        count++;
        if (count > 500) {
            Debug.LogError("INFINITE FOLDER CHECK!");
            return;
        }
        
        var filePaths = Directory.GetFiles(folderPath);
        foreach (var filePath in filePaths) {
            if (Path.GetExtension(filePath) == "." + filetype) {
                string relativePath =  "Assets" + filePath.Substring(Application.dataPath.Length);
                packCallback(relativePath);
            }
        }

        foreach (var directory in Directory.GetDirectories(folderPath)) {
            GetAccessoriesInFolder(ref count, directory, filetype, packCallback);
        }
    }

    [MenuItem("Assets/Create/Airship/Accessories/Generate Materials In Folder")]
    static void GenerateMaterialsInFolder(){
        var processedFiles = new Dictionary<string, Material>();
        foreach (var obj in Selection.objects) {
            string selectionPath = AssetDatabase.GetAssetPath(obj); // relative path
            if (Directory.Exists(selectionPath)) {
                //This is a folder
                foreach(var file in Directory.GetFiles(selectionPath)){
                    //Debug.Log("Found File: " + file);
                    if(Path.GetExtension(file) != ".png"){
                        continue;
                    }
                    var fileKey = Path.GetFileName(file).Split('_')[0];
                    if(!processedFiles.ContainsKey(fileKey)){
                        Debug.Log("Grabbing texture files for: " + fileKey);

                        //Get all the textures we need
                        //Debug.Log("Getting diffuse texture: " + Path.Combine(selectionPath,fileKey + "_Albedo.png"));
                        var textureDiffuse = (Texture2D)AssetDatabase.LoadAssetAtPath(Path.Combine(selectionPath,fileKey + "_Albedo.png"), typeof(Texture2D));
                        var textureMetal = (Texture2D)AssetDatabase.LoadAssetAtPath(Path.Combine(selectionPath,fileKey + "_Metalness.png"), typeof(Texture2D));
                        var textureNormal = (Texture2D)AssetDatabase.LoadAssetAtPath(Path.Combine(selectionPath,fileKey + "_Normals.png"), typeof(Texture2D));
                        
                        //Create the material and assign the textures
                        var newMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        if(textureDiffuse)
                            newMaterial.SetTexture("_BaseMap", textureDiffuse);
                        if(textureNormal)
                            newMaterial.SetTexture("_BumpMap", textureNormal);
                        if(textureMetal)
                            newMaterial.SetTexture("_MetallicGlossMap", textureMetal);

                        //Save the Material into the folder
                        AssetDatabase.CreateAsset(newMaterial, Path.Combine(selectionPath, fileKey+".mat"));


                        processedFiles.Add(fileKey, newMaterial);
                    }
                }
            }
        }
    }

    [MenuItem("Assets/Create/Airship/Accessories/Generate Materials In Folder", true)]
    private static bool ValidateGenerateMaterialsInFolder(){
        foreach (var obj in Selection.objects) {
            string selectionPath = AssetDatabase.GetAssetPath(obj); // relative path
            if (Directory.Exists(selectionPath)) {
                //This is a folder
                return true;
            }
        }
        return false;
    }


    [MenuItem("Airship/Avatar/Create Outfit Accessories from Mesh %f8", true)]
    [MenuItem("Assets/Create/Airship/Accessories/Create Outfit Accessories from Mesh", true)]
    private static bool ValidateCreateAccFromMesh(){
        return Selection.GetFiltered<GameObject>(AssetModeMask).Length > 0;
    }

    [MenuItem("Airship/Avatar/Create Outfit Accessories from Mesh %f8")]
    [MenuItem("Assets/Create/Airship/Accessories/Create Outfit Accessories from Mesh")]
    static void CreateAccFromMesh(){
        processedPaths.Clear();
        defaultMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/AirshipPackages/@Easy/CoreMaterials/MaterialLibrary/Clay.mat");
        var objects = Selection.GetFiltered<GameObject>(AssetModeMask);
        foreach(var obj in objects){
            Debug.Log("Unpacking: " + obj.name);
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if(renderers.Length > 0){
                UnpackRenderers(obj, renderers);
            }
        }
    }

    [MenuItem("Airship/Avatar/Create Accessory from Mesh %f8", true)]
    [MenuItem("Assets/Create/Airship/Accessories/Create Accessory from Mesh", true)]
    private static bool ValidateCreateSingleAccFromMesh(){
        return Selection.GetFiltered<GameObject>(AssetModeMask).Length == 1;
    }

    [MenuItem("Airship/Avatar/Create Accessory from Mesh %f8")]
    [MenuItem("Assets/Create/Airship/Accessories/Create Accessory from Mesh")]
    static void CreateSingleAccFromMesh(){
        processedPaths.Clear();
        defaultMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/AirshipPackages/@Easy/CoreMaterials//MaterialLibrary/Organic/Clay.mat");
        UnpackSingleObject(Selection.GetFiltered<GameObject>(AssetModeMask)[0]);
    }

    private static void UnpackSingleObject(GameObject rootGo){
        string rootPath = AssetDatabase.GetAssetPath(rootGo.GetInstanceID());
        string fileName = Path.GetFileNameWithoutExtension(rootPath);
        string accPrefabPath = Path.Combine(Path.GetDirectoryName(rootPath), fileName+".prefab");

        if (File.Exists(accPrefabPath)) {
            var acceptsOverwrite = EditorUtility.DisplayDialog(fileName+".prefab already exists. Do you want to replace it?",
                "A prefab already exists with this name. Replacing it will overwrite the existing prefab. This can't be undone.", "Replace", "Cancel");
            if (!acceptsOverwrite) {
                return;
            }
        }
        
        //Load the mesh into a prefab
        var accInstance = (GameObject)PrefabUtility.InstantiatePrefab(rootGo);
        accInstance.name = rootGo.name; // Remove (Clone)
        PrefabUtility.UnpackPrefabInstance(accInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var accComponent = accInstance.AddComponent<AccessoryComponent>();
        accComponent.skinnedToCharacter = accInstance.GetComponentInChildren<SkinnedMeshRenderer>() != null;
        // Always default to right hand for reliability
        var slot = AccessorySlot.RightHand; // GetSlot(accInstance.name, accComponent.skinnedToCharacter);
        accComponent.accessorySlot = slot;
        
        foreach(var ren in accInstance.GetComponentsInChildren<Renderer>()){
            if(!ren){
                continue;
            }
            //Assign a default material
            var materials = ren.sharedMaterials;
            for(int i=0; i<ren.sharedMaterials.Length; i++){
                materials[i] = defaultMat;
            }
            ren.sharedMaterials = materials;
            ren.gameObject.AddComponent<MaterialColorURP>();
        }
        
        //Save the prefab
        PrefabUtility.SaveAsPrefabAsset(accInstance, accPrefabPath);
        GameObject.DestroyImmediate(accInstance);

        EditorUtility.DisplayDialog("Accessory Created", $"Created accessory {fileName}\nUsing slot: {slot}", "OK");
    }

    private static void UnpackRenderers(GameObject rootGo, Renderer[] renderers){
        //Validate the asset is useable
        string rootPath = AssetDatabase.GetAssetPath(rootGo.GetInstanceID());
        if(processedPaths.Contains(rootPath)){
            Debug.LogWarning("Skipping duplicate file attempt: " + rootGo.name);
            return;
        }

        Type[] types = AssetDatabase.GetAvailableImporters(rootPath);
        bool useableType = false;
        foreach(var t in types){
            Debug.Log("-TYPE: " + t);
            if(t == typeof(ModelImporter)){
                useableType = true;
                break;
            }
        }
        if(!useableType){
            Debug.LogWarning("Unable to convert: " + rootGo +" because it is not a model");
            return;
        }
        string fileName = Path.GetFileNameWithoutExtension(rootPath);
        string allAccPrefabPath = Path.Combine(Path.GetDirectoryName(rootPath), fileName+".prefab");

        Debug.Log("Creating file at: "+ allAccPrefabPath);

        //Load the mesh into a prefab
        var allAccInstance = (GameObject)PrefabUtility.InstantiatePrefab(rootGo);
        allAccInstance.name = rootGo.name; // Remove (Clone)
        PrefabUtility.UnpackPrefabInstance(allAccInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAsset(allAccInstance, allAccPrefabPath);

        int nestedAccCount = 0;

        var allAccs = new List<AccessoryComponent>();
        foreach(var ren in allAccInstance.GetComponentsInChildren<Renderer>()){
            if(!ren){
                continue;
            }
            //Assign a default material
            Debug.Log("Using mat: " + defaultMat.name);
            var materials = ren.sharedMaterials;
            for(int i=0; i<ren.sharedMaterials.Length; i++){
                materials[i] = defaultMat;
            }
            ren.sharedMaterials = materials;
            ren.gameObject.AddComponent<MaterialColorURP>();

            //Asssing the accessory
            var acc = ren.gameObject.AddComponent<AccessoryComponent>();
            acc.skinnedToCharacter = ren.GetType() == typeof(SkinnedMeshRenderer);
            acc.accessorySlot = GetSlot(ren.gameObject.name, acc.skinnedToCharacter);

            //Create a prefab for each accessory
            if(ren.gameObject != allAccInstance.gameObject){
                var accGo = GameObject.Instantiate(ren.gameObject);
                accGo.name = ren.gameObject.name; // Remove (Clone)
                string individualAccPrefabPath = Path.Combine(Path.GetDirectoryName(rootPath), accGo.name+".prefab");
                var individualAccTemplate = PrefabUtility.SaveAsPrefabAsset(accGo, individualAccPrefabPath);
                allAccs.Add(individualAccTemplate.GetComponent<AccessoryComponent>());

                //Replace the renderer with the nested prefab
                var accInstance = (GameObject)PrefabUtility.InstantiatePrefab(individualAccTemplate);
                accInstance.name = individualAccTemplate.name; // Remove (Clone)
                accInstance.transform.parent = ren.transform.parent;
                var skinnedInstance = accInstance.GetComponent<SkinnedMeshRenderer>();
                if(skinnedInstance){
                    var oldSkin = ren as SkinnedMeshRenderer;
                    skinnedInstance.rootBone = oldSkin.rootBone;
                    skinnedInstance.bones = oldSkin.bones;
                }
                GameObject.DestroyImmediate(ren.gameObject);
                GameObject.DestroyImmediate(accGo);
                nestedAccCount++;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(allAccInstance, allAccPrefabPath);
        Undo.RegisterCreatedObjectUndo(allAccInstance, "Create " + allAccInstance.name);

        //Create an outfit asset
        if(nestedAccCount > 0){
            string outfitPath = Path.Combine(Path.GetDirectoryName(rootPath), rootGo.name + "_Outfit.asset");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AccessoryOutfit>(), outfitPath);
            var outfit = AssetDatabase.LoadAssetAtPath<AccessoryOutfit>(outfitPath);
            outfit.accessories = allAccs.ToArray();
        }

        //Load the accessories into the avatar collection to show in the avatar editor
        FillAvatarCollection();

        //Save changes
        AssetDatabase.SaveAssets();

        //Cleanup
        GameObject.DestroyImmediate(allAccInstance);
    }

    //Guess the avatar slot based on the name
    private static AccessorySlot GetSlot(string name, bool skinnedMesh){
        string lower = name.ToLower().Replace("_", "").Replace(" ", "");

        //TODO: For non skinned meshes I need to evaulate if it is a Left or Right version of things like hands and feet

        if(lower.Contains("torso") || 
        lower.Contains("body") || 
        lower.Contains("top") || 
        lower.Contains("shirt") || 
        lower.Contains("jacket")){
            return AccessorySlot.Torso;
        }

        if(lower.Contains("hand") || 
        lower.Contains("arms") || 
        lower.Contains("glove") || 
        lower.Contains("watch") ){
            if(skinnedMesh){
                return AccessorySlot.Hands;
            }else{
                 if(lower.Contains("handR") || 
                    lower.Contains("armsR") || 
                    lower.Contains("gloveR") || 
                    lower.Contains("watchR") ||
                    lower.Contains("Rhand") || 
                    lower.Contains("Rarms") || 
                    lower.Contains("Rglove") || 
                    lower.Contains("Rwatch")){
                        return AccessorySlot.RightHand;
                }
                    
                if(lower.Contains("handL") || 
                    lower.Contains("armsL") || 
                    lower.Contains("gloveL") || 
                    lower.Contains("watchL") ||
                    lower.Contains("Lhand") || 
                    lower.Contains("Larms") || 
                    lower.Contains("Lglove") || 
                    lower.Contains("Lwatch")){
                        return AccessorySlot.RightHand;
                }

                return AccessorySlot.Hands;
            }
        }

        if(lower.Contains("leg") || 
        lower.Contains("pant") || 
        lower.Contains("underwear") || 
        lower.Contains("jeans")|| 
        lower.Contains("short") ){
            return AccessorySlot.Legs;
        }

        if(lower.Contains("back") || 
        lower.Contains("pack") || 
        lower.Contains("purse")) {
            return AccessorySlot.Backpack;
        }

        if(lower.Contains("shoe") || 
        lower.Contains("boot") || 
        lower.Contains("feet") || 
        lower.Contains("foot")|| 
        lower.Contains("sandal") ){
            return AccessorySlot.Feet;
        }
        
        if(lower.Contains("hair") || 
        lower.Contains("mohawk") || 
        lower.Contains("pony") || 
        lower.Contains("braid")|| 
        lower.Contains("wig") ){
            return AccessorySlot.Hair;
        }
        
        if(lower.Contains("ear") || 
        lower.Contains("stud") || 
        lower.Contains("hoop") || 
        lower.Contains("dangle")){
            return AccessorySlot.Ears;
        }
        
        if(lower.Contains("head") || 
        lower.Contains("hat") || 
        lower.Contains("face") || 
        lower.Contains("goggle") || 
        lower.Contains("glass")|| 
        lower.Contains("mono") ){
            return AccessorySlot.Head;
        }
        

        return skinnedMesh ? AccessorySlot.Root : AccessorySlot.RightHand;
    }
}
