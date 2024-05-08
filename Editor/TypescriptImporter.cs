using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Airship.Editor;
using Luau;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Editor {
    [UnityEditor.AssetImporters.ScriptedImporter(1, "ts")]
    public class TypescriptImporter : LuauImporter {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptOk.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        private static bool _isCompiling = false;
        
        private static readonly List<Luau.BinaryFile> CompiledFiles = new();
        private static readonly Stopwatch Stopwatch = new();
        private static readonly Stopwatch StopwatchCompile = new();

        private static TypescriptConfig _rootConfig;
        public static TypescriptConfig TypescriptConfig {
            get {
                if (_rootConfig != null) {
                    return _rootConfig;
                }
                
                _rootConfig = TypescriptConfig.ReadTsConfig("Assets");
                return _rootConfig;
            }
        }
        
        // internal static string TransformOutputPath(string inputPath) {
        //     foreach (var rootDir in RootConfig.RootDirs) {
        //         if (!inputPath.StartsWith(rootDir)) continue;
        //         
        //         var output = inputPath.Replace(rootDir, RootConfig.OutDir);
        //         return output.Replace(".ts", ".lua");
        //     }
        //
        //     return Path.Join(RootConfig.OutDir, inputPath.Replace(".ts", ".lua")).Replace("\\", "/");
        // }

        private void CompileTypescriptAsset(AssetImportContext ctx, TypescriptFile file) {
            
        }
        
        public override void OnImportAsset(AssetImportContext ctx) {
            var typescriptIconPath = IconEmpty;
  

            var typescriptAsset = ScriptableObject.CreateInstance<Luau.TypescriptFile>();
            typescriptAsset.path = ctx.assetPath;
            var ext = Path.GetExtension(ctx.assetPath);
            var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
            


            if (Directory.Exists(TypescriptConfig.Directory)) {
                var outPath = TypescriptConfig.GetOutputPath(ctx.assetPath);
                if (File.Exists(outPath)) {
                    var binaryFile = CompileLuauAsset(ctx, outPath);
                    binaryFile.name = Path.GetFileNameWithoutExtension(outPath);
                    typescriptAsset.binaryFile = binaryFile;
                    typescriptIconPath = IconOk;
                }
                else {
                    CompileTypescriptAsset(ctx, typescriptAsset);
                }
            }
            
            var typescriptIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(typescriptIconPath);
            ctx.AddObjectToAsset(fileName + ".ts_asset", typescriptAsset, typescriptIcon);
            ctx.SetMainObject(typescriptAsset); // ??
        }
    }
}