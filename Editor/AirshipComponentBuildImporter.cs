using System.IO;
using Luau;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor {
    [ScriptedImporter(1, "asbuildinfo")]
    public class AirshipComponentBuildImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            var data = File.ReadAllText(ctx.assetPath);

            var airshipBuild = ScriptableObject.CreateInstance<AirshipBuildInfo>();
            airshipBuild.data = AirshipBuildData.FromJsonData(data);
            
            ctx.AddObjectToAsset("build", airshipBuild);
            ctx.SetMainObject(airshipBuild);
        }
    }
}
