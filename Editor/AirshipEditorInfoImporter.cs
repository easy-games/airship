using System.IO;
using Luau;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor {
    [ScriptedImporter(1, "aseditorinfo")]
    public class AirshipEditorInfoImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            var data = File.ReadAllText(ctx.assetPath);

            var airshipEditorData = ScriptableObject.CreateInstance<AirshipEditorInfo>();
            airshipEditorData.editorMetadata = EditorMetadataJson.FromJsonData(data);
            
            ctx.AddObjectToAsset("editorMetadata", airshipEditorData);
            ctx.SetMainObject(airshipEditorData);
        }
    }
}