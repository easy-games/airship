using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System;

public class AccessoryCollectionTools {
    private const SelectionMode AssetModeMask = SelectionMode.Assets | SelectionMode.TopLevel | SelectionMode.DeepAssets;
    private static List<string> processedPaths = new List<string>();
    private static Material defaultMat; 

    [MenuItem("Airship/Avatar/Fill All Avatar Accessories")]
    private static void FillAvatarCollection() {
        Debug.Log("Grabbing all avatar accessories");
        string folderPath = Application.dataPath + "/Bundles/@Easy/Core/Shared/Resources/Accessories/AvatarItems";
        string allItemsPath
            = "Assets/Bundles/@Easy/Core/Shared/Resources/Accessories/AvatarItems/EntireAvatarCollection.asset";
        AvatarAccessoryCollection allAccessories = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(allItemsPath);

        //Compile accessories
        List<AccessoryComponent> accs = new List<AccessoryComponent>();
        int count = 0;
        GetAccessoriesInFolder(ref count, folderPath, "prefab", (relativePath)=>{
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                var acc = go.GetComponent<AccessoryComponent>();
                if (acc != null) {
                    Debug.Log("Found Accessory: " + relativePath);
                    accs.Add(acc);
                }
        });
        allAccessories.accessories = accs.ToArray();

        //Compile faces
        List<AccessoryFace> faces = new List<AccessoryFace>();
        count = 0;
        GetAccessoriesInFolder(ref count, folderPath, "asset", (relativePath)=>{
                var face = AssetDatabase.LoadAssetAtPath<AccessoryFace>(relativePath);
                if (face != null) {
                    Debug.Log("Found Face: " + relativePath);
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

    [MenuItem("Airship/Avatar/Create Avatar Accessories From Mesh %f8", true)]
    [MenuItem("Assets/Create/Airship/Accessories/Create Avatar Accessories From Mesh", true)]
    private static bool ValidateCreateAccFromMesh(){
        return Selection.GetFiltered<GameObject>(AssetModeMask).Length > 0;
    }

    [MenuItem("Airship/Avatar/Create Avatar Accessories From Mesh %f8")]
    [MenuItem("Assets/Create/Airship/Accessories/Create Avatar Accessories From Mesh")]
    static void CreateAccFromMesh(){
        Debug.Log("Creating accessories from meshes");
        processedPaths.Clear();
        defaultMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bundles/@Easy/CoreMaterials/Shared/Resources/MaterialLibrary/Organic/Clay.mat");
        var objects = Selection.GetFiltered<GameObject>(AssetModeMask);
        foreach(var obj in objects){
            Debug.Log("Unpacking: " + obj.name);
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if(renderers.Length > 0){
                UnpackRenderers(obj, renderers);
            }
        }
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
        allAccInstance.name = allAccInstance.name.Split("(Clone)")[0];
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
            ren.gameObject.AddComponent<MaterialColor>();

            //Asssing the accessory
            var acc = ren.gameObject.AddComponent<AccessoryComponent>();
            acc.skinnedToCharacter = ren.GetType() == typeof(SkinnedMeshRenderer);
            acc.accessorySlot = GetSlot(ren.gameObject.name, acc.skinnedToCharacter);

            //Create a prefab for each accessory
            if(ren.gameObject != allAccInstance.gameObject){
                var accGo = GameObject.Instantiate(ren.gameObject);
                accGo.name = accGo.name.Split("(Clone)")[0];
                string individualAccPrefabPath = Path.Combine(Path.GetDirectoryName(rootPath), accGo.name+".prefab");
                var individualAccTemplate = PrefabUtility.SaveAsPrefabAsset(accGo, individualAccPrefabPath);
                allAccs.Add(individualAccTemplate.GetComponent<AccessoryComponent>());

                //Replace the renderer with the nested prefab
                var accInstance = (GameObject)PrefabUtility.InstantiatePrefab(individualAccTemplate);
                accInstance.name = accInstance.name.Split("(Clone)")[0];
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
        string lower = name.ToLower();

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
            return AccessorySlot.Hands;
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
        

        return AccessorySlot.Root;
    }
}
