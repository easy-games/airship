using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReferenceBuilder{
    public static class ReferenceBuilderSerializer {
        private const string PathToCompiledScript
            = "Bundles/@Easy/Core/Core~/src/Shared/Util/ReferenceManagerResources.ts";
        //Airship-Bedwars\Assets\Bundles\@Easy\Core\Core~\src\Shared\Util\ReferenceManagerResources.ts
        private const string ConstantDeclarations
            = "export interface BundleGroup{ \n\t" +
              "id:BundleGroupNames; \n\t" +
              "bundles:Map<number, BundleData>;\n" +
              "}\n\n" +
              "export interface BundleData{\n\t" +
              "id:Number;\n\t" +
              "filePaths:Map<number, string>;\n" +
              "}";
        private const string ClassStart ="export class ReferenceManagerAssets{" ;        
        private const string ClassEnd = "\n}";
        private const string PathToAssets = "ReferenceBuilderAssets";
        private const string BundleEnumName = "BundleGroupNames";
        private const string AllItemEnumName = "AllBundleItems";
        private const string AssetArrayStart = "\n\tpublic static readonly bundleGroups:Map<number, BundleGroup> = new Map([";
        private const string AssetArrayEnd = "\n\t]);";
        
        private static string GetFilePath() {
            return Path.Combine(Application.dataPath, PathToCompiledScript);
        }

        private static string GetPathTo(Object obj) {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) {
                return "";
            }
            int resourcesIndex = path.IndexOf("AirshipPackages")+8;
            return path.Substring(resourcesIndex);
        }

        private static void SaveFile(string text) {
            File.WriteAllText(GetFilePath(),text);
        }

        public static void Compile() {
            Debug.Log("<color=yellow>Reference Builder Compiling all assets into Typescript</color>");
            //Get all reference assets so we can combine them in one script
            var assets = Resources.LoadAll(PathToAssets, typeof(ReferenceBuilderAsset));
            string classText = ClassStart;
            string assetArrayText = AssetArrayStart;
            string enumText = "";
            string[] assetNames = new string[assets.Length];
            List<string> allItemKeys = new List<string>();
            List<string> allItemValues = new List<string>();
            
            //Serialize each asset
            for (var assetI = 0; assetI < assets.Length; assetI++) {
                var asset = (ReferenceBuilderAsset)assets[assetI];
                string[] bundleNames = new string[asset.bundles.Count];
                string bundleEnumName = "Bundle_" + asset.referenceId;
                //Serialize the Asset
                classText += SerializeAssetStart(asset);
                
                
                //Loop through each bundle in this asset
                for (int bundleI = 0; bundleI < asset.bundles.Count; bundleI++) {
                    var bundle = asset.bundles[bundleI];
                    Debug.Log("Writing Bundle: " + bundleI + " " + bundle.key);
                    bundleNames[bundleI] = bundle.key;
                    string[] itemKeys = new string[bundle.value.Count];
                    string bundleItemEnumName = $"Bundle_{asset.referenceId}_{bundle.key}";
                    
                    //Serialize the start of this asset map
                    
                    //Serialize the Bundle
                    classText += SerializeAssetItem($"Bundle_{asset.referenceId}.{bundle.key}");
                    classText += SerializeBundleStart(bundleEnumName, bundle.key);
                    
                    //Loop through all items in this bundle
                    for (int itemI = 0; itemI < itemKeys.Length; itemI++) {
                        var item = bundle.value[itemI];
                        itemKeys[itemI] = item.key;
                        
                        //Serialize the item
                        var itemPath = GetPathTo(item.value);
                        
                        classText += SerializeBundleItem($"{bundleItemEnumName}.{item.key}", itemPath);
                        
                        allItemKeys.Add($"{asset.referenceId}_{bundle.key}_{item.key}");
                        allItemValues.Add($"\"{itemPath}\"");
                    }

                    //Close the bundle
                    classText += BundleEnd;
                    
                    //Serialize the enum values for each item in this bundle
                    enumText += SerializeEnum(bundleItemEnumName, itemKeys);
                    
                }
                //Create an array of all the bundles for easy access
                Debug.Log("Writing Bundle Array Asset: " + asset.referenceId);
                assetArrayText += SerializeAssetArrayItem(asset.referenceId);
                
                //Close the asset 
                classText += AssetEnd;
                
                assetNames[assetI] = asset.referenceId;
                
                //Serialize the enum that stores each aset id
                enumText += SerializeEnum(bundleEnumName, bundleNames);

            }

            //Serialize an enum that contains every individual item
            enumText += SerializeEnum(AllItemEnumName, allItemKeys.ToArray(), allItemValues.ToArray());

            //Close the asset array
            assetArrayText += AssetArrayEnd;
            
            //Add the array to the class and end the class
            classText += assetArrayText + ClassEnd;
            
            //enum of asset keys
            enumText += SerializeEnum(BundleEnumName, assetNames);
            
            //Save final file
            string finalText = $"{ConstantDeclarations}\n\n{enumText}\n\n{classText}";
            //Debug.Log($"FILE PATH: {GetFilePath()}\n EXPORTED TEXT: \n {finalText}");
            SaveFile(finalText);

            Debug.Log("<color=green>Reference Builder Completed!</color> Generated TS at: " + GetFilePath());
        }

        
        /*Final Asset Code
            public static readonly ItemBlock:BundleGroup = {
		        id: BundleGroupNames.ItemBlock,
		        bundlesGroup: [{
			        id: Bundle_ItemBlock.FIRST_PERSON,
			        filePaths: new Map([
				        [Bundle_ItemBlock_FirstPerson.Idle, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Root/character_rig.004_FP_Idle.anim"],
				        [Bundle_ItemBlock_FirstPerson.Equip, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_Equip.anim"],
				        [Bundle_ItemBlock_FirstPerson.Unequip, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_UnEquip.anim"],
				        [Bundle_ItemBlock_FirstPerson.Use01, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_IdleFlare.anim"],
			        ])},{
			        id: Bundle_ItemBlock.THIRD_PERSON,
			        filePaths: new Map([
				        [Bundle_ItemBlock_ThirdPerson.Idle, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Root/character_rig.004_FP_Idle.anim"],
				        [Bundle_ItemBlock_ThirdPerson.Equip, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_Equip.anim"],
				        [Bundle_ItemBlock_ThirdPerson.Unequip, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_UnEquip.anim"],
				        [Bundle_ItemBlock_ThirdPerson.Use01, "Shared/Resources/Entity/HumanEntity/HumanAnimations/Items/Unarmed/character_rig.004_FP_Unarmed_IdleFlare.anim"],
			        ])},
		        ]
	        }
        */
        private static string SerializeAssetStart(ReferenceBuilderAsset asset) {
            //Create a variable that stores the mapping for each bundle group
            return $"\n\tpublic static readonly {asset.referenceId}:BundleGroup = {{\n\t\t" +
                       $"id: {BundleEnumName}.{asset.referenceId},\n\t\t" +
                       $"bundles: new Map([";
        }

        private static string SerializeAssetItem(string assetEnum) {
            return $"\n\t\t[{assetEnum}, ";
        }
        private const string AssetEnd = "\n\t\t])\n\t}\n";

        private static string SerializeBundleStart(string bundleEnum, string bundleKey) {
            return $"{{\n\t\t\t" +
                   $"id: {bundleEnum}.{bundleKey},\n\t\t\t" +
                   $"filePaths: new Map([\n";
        }

        private static string SerializeBundleItem(string bundleItemEnum, string path) {
            return $"\t\t\t\t[{bundleItemEnum}, \"{path}\"],\n";
        }
        
        private  const string BundleEnd = "\t\t\t])}],";

        /*
            public static readonly bundleGroups:Map<number, BundleGroup> = new Map([
		        [BundleGroupNames.ItemSword, ReferenceManagerAssets.ItemSword],
	        ])
	    */
        private static string SerializeAssetArrayItem(string id) {
            return $"\n\t\t[{BundleEnumName}.{id}, ReferenceManagerAssets.{id}],";
        }
        
        private static string SerializeMapItem(string key, string value) {
            return $"\n\t\t[{key}, {value}],";
        }
            
        private static string SerializeEnum(string name, string[] values) {
            string text = $"export enum {name}{{\n\tNONE = -1,\n";
            for (int i = 0; i < values.Length; i++) {
                text += $"\t{values[i]},\n";
            }
            text += "}\n\n";
            return text;
        }
            
        private static string SerializeEnum(string name, string[] keys, string[] values) {
            string text = $"export enum {name}{{\n\tNONE = -1,\n";
            for (int i = 0; i < values.Length; i++) {
                text += $"\t{keys[i]} = {values[i]},\n";
            }
            text += "}\n\n";
            return text;
        }
    }
}
