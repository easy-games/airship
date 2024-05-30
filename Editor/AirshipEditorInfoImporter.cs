using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Luau;
using UnityEditor;
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
            
            // AssetDatabase.StartAssetEditing();
            // var hashes = airshipEditorData.editorMetadata.FileHashes;
            // foreach (var fileHash in hashes) {
            //     var fileLocation = "Assets/" + fileHash.Key;
            //     var compileTimeHash = fileHash.Value.Hash;
            //     using var crypto = new SHA1CryptoServiceProvider();
            //     string hash = BitConverter.ToString(crypto.ComputeHash(File.ReadAllBytes(fileLocation)));
            //     if (hash != compileTimeHash) {
            //         AssetDatabase.ImportAsset(fileLocation, ImportAssetOptions.Default);
            //     }
            // }
            // AssetDatabase.StopAssetEditing();
            
            AirshipEditorInfo.useEnumCache = false;
         }
    }
}