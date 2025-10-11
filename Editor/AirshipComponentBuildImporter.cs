using System;
using System.IO;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor {
    [ScriptedImporter(1, "asbuildinfo")]
    public class AirshipComponentBuildImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            if (AirshipBuildInfo.PrimaryAssetPath == ctx.assetPath) {
                AirshipBuildInfo.ClearInstance();
            }
            
            var data = File.ReadAllText(ctx.assetPath);
            var airshipBuild = ScriptableObject.CreateInstance<AirshipBuildInfo>();
            airshipBuild.data = AirshipBuildData.FromJsonData(data);
            ctx.AddObjectToAsset("build", airshipBuild);
            ctx.SetMainObject(airshipBuild);

            // if (AirshipBuildInfo.PrimaryAssetPath == ctx.assetPath) {
            //     EditorApplication.delayCall += () => {
            //         AirshipCustomEditors.RegisterCustomEditors2();
            //     };
            // }
        }
    }

    public class AirshipTypePostProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {
            if (importedAssets.Contains(AirshipBuildInfo.PrimaryAssetPath)) {
                AirshipCustomEditors.RegisterEditorsForRegisteredTypes();
            }
        }
    }
}
