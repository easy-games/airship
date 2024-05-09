using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor {
    [ScriptedImporter(1, "ts")]
    public class TypescriptImporter : LuauImporter {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAssetOk.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptAssetOff.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptAssetErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        private static bool _isCompiling = false;
        
        private static readonly List<Luau.BinaryFile> CompiledFiles = new();
        private static readonly Stopwatch Stopwatch = new();
        private static readonly Stopwatch StopwatchCompile = new();

        private static TypescriptConfig _projectConfig;
        public static TypescriptConfig ProjectConfig {
            get {
                if (_projectConfig != null) return _projectConfig;
                
                var directory = Path.GetDirectoryName(EditorIntegrationsConfig.instance.typescriptProjectConfig);
                var file = Path.GetFileName(EditorIntegrationsConfig.instance.typescriptProjectConfig);
                
                return TypescriptConfig.FindTsConfig(directory, out _projectConfig, file) ? _projectConfig : null;
            }
        }

        [MenuItem("Airship/Misc/Reimport Typescript Files")]
        public static void ReimportAllTypescript() {
            _projectConfig = null; // force tsconfig refresh
            
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            foreach (var file in Directory.EnumerateFiles("Assets", "*.ts", SearchOption.AllDirectories)) {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
            }
            AssetDatabase.StopAssetEditing();
        }

        private void CompileTypescriptAsset(AssetImportContext ctx, string file) {
            // TypescriptCompilationService.RequestCompileFile(Path.GetFullPath(file));
        }
        
        public override void OnImportAsset(AssetImportContext ctx) {
            var airshipScript = ScriptableObject.CreateInstance<Luau.BinaryFile>();
            airshipScript.scriptLanguage = AirshipScriptLanguage.Typescript;
            airshipScript.assetPath = ctx.assetPath;

            var project = TypescriptProjectsService.Project;
            
            var typescriptIconPath = IconEmpty;
            var ext = Path.GetExtension(ctx.assetPath);
            var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
            
            if (Directory.Exists(ProjectConfig.Directory)) {
                // CompileTypescriptAsset(ctx, ctx.assetPath);
                
                var outPath = project.GetOutputPath(ctx.assetPath);
                if (File.Exists(outPath)) {
                    typescriptIconPath = IconOk;
                    var (_, result) = CompileLuauAsset(ctx, airshipScript, outPath);
                    if (!result.Value.Compiled) {
                        typescriptIconPath = IconFail;
                    }
                }
            }
            else {
                typescriptIconPath = IconFail;
            }
            
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(typescriptIconPath);
            ctx.AddObjectToAsset(fileName, airshipScript, icon);
            ctx.SetMainObject(airshipScript);
        }
    }
}