using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Airship.Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Editor {
    public class TypescriptPostProcessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {

            Profiler.BeginSample("TypescriptPostProcessor");
            for (int i = 0; i < movedAssets.Length; i++) {
                var targetPath = movedAssets[i];
                var fromPath = movedFromAssetPaths[i];
    
                // If a typescript file was "moved" - we can then handle the rename event for it here!
                if (targetPath.EndsWith(".ts")) {
                    TypescriptProjectsService.HandleRenameEvent(fromPath, targetPath);
                }
            }
            Profiler.EndSample();
        }
    }
    
    // Queue offset set low to occur before prefabs. It can be changed freely, although should ideally
    // remain before prefab imports.
    [ScriptedImporter(1, "ts", -10000000)]
    public class TypescriptImporter : LuauImporter {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconDeclaration = "Packages/gg.easy.airship/Editor/TypescriptAssetDeclaration.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptAssetUncompiled.png";
        private const string IconFail = "Packages/gg.easy.airship/Editor/TypescriptAssetErr.png";
        
        private const string LuaIconOk = "Packages/gg.easy.airship/Editor/LuauIcon.png";
        private const string LuaIconFail = "Packages/gg.easy.airship/Editor/LuauErrorIcon.png";
        
        private static bool _isCompiling = false;
        
        private static readonly List<Luau.AirshipScript> CompiledFiles = new();
        private static readonly Stopwatch Stopwatch = new();
        private static readonly Stopwatch StopwatchCompile = new();

        private static TypescriptConfig _projectConfig;
        public static TypescriptConfig ProjectConfig {
            get {
                if (_projectConfig != null) return _projectConfig;
                
                var directory = Path.GetDirectoryName(TypescriptProjectsService.ProjectPath);
                var file = Path.GetFileName(TypescriptProjectsService.ProjectPath);
                
                return TypescriptConfig.FindInDirectory(directory, out _projectConfig, file) ? _projectConfig : null;
            }
        }
        
        [MenuItem("Airship/TypeScript/Reimport Scripts")]
        public static void ReimportAllTypescript() {
            ReimportAllLuau();
            _projectConfig = null; // force tsconfig refresh
            
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            foreach (var file in Directory.EnumerateFiles("Assets", "*.ts", SearchOption.AllDirectories)) {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
            }
            AssetDatabase.StopAssetEditing();
        }

        public static bool RequiresRecompile(string assetPath) {
            var asset = AssetDatabase.LoadAssetAtPath<AirshipScript>(assetPath);
            if (asset == null) return true;
            
            var project = TypescriptProjectsService.Project;
            var currentHash = project.GetOutputFileHash(assetPath);
            
            return currentHash != asset.compiledFileHash;
        }

        public override void OnImportAsset(AssetImportContext ctx) {
            if (FileExtensions.EndsWith(ctx.assetPath,FileExtensions.TypescriptDeclaration)) {
                var airshipScript = ScriptableObject.CreateInstance<Luau.DeclarationFile>();
                var source = File.ReadAllText(ctx.assetPath);
                airshipScript.ambient = !source.Contains("export ");

                var declarationForFile = FileExtensions.Transform(ctx.assetPath, FileExtensions.TypescriptDeclaration, FileExtensions.Lua);
                if (File.Exists(declarationForFile)) {
                    airshipScript.isLuauDeclaration = true;
                    airshipScript.scriptPath = declarationForFile;
                }
                
                var ext = Path.GetExtension(ctx.assetPath);
                var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDeclaration);
                ctx.AddObjectToAsset(fileName, airshipScript, icon);
                ctx.SetMainObject(airshipScript);
            }
            else {
                var hasCompiled = false;
                var airshipScript = ScriptableObject.CreateInstance<Luau.AirshipScript>();
                airshipScript.scriptLanguage = AirshipScriptLanguage.Typescript;
                airshipScript.assetPath = ctx.assetPath;
                
                var project = TypescriptProjectsService.Project;
                var ext = Path.GetExtension(ctx.assetPath);
                var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
            
                var typescriptIconPath = IconEmpty;
                if (project != null && Directory.Exists(ProjectConfig.Directory)) {
                    var outPath = project.GetOutputPath(ctx.assetPath);
                    if (File.Exists(outPath)) {
                        typescriptIconPath = IconOk;
                        hasCompiled = true;
                        var (_, result) = CompileLuauAsset(ctx, airshipScript, outPath);
                        if (!result.Value.Compiled) {
                            typescriptIconPath = IconFail;
                            hasCompiled = false;
                        }
                    }
                }
                else {
                    typescriptIconPath = IconFail;
                }

                Texture2D icon;
                if (airshipScript.m_metadata?.displayIcon != null && hasCompiled) {
                    icon = airshipScript.m_metadata.displayIcon;
                }
                else {
                    icon = AssetDatabase.LoadAssetAtPath<Texture2D>(typescriptIconPath);
                }

                airshipScript.typescriptWasCompiled = hasCompiled;
                airshipScript.compiledFileHash = project.GetOutputFileHash(assetPath);
                ctx.AddObjectToAsset(fileName, airshipScript, icon);
                ctx.SetMainObject(airshipScript);
                
                if (AirshipReconciliationService.ReconcilerVersion == ReconcilerVersion.Version2 && airshipScript.airshipBehaviour) {
                    var assetData = AirshipLocalArtifactDatabase.instance.GetOrCreateScriptAssetData(airshipScript);
                    
                    if (assetData.metadata == null || airshipScript.sourceFileHash != assetData.metadata.hash) {
                        assetData.metadata = new TypescriptCompilerMetadata() {
                            hash = airshipScript.sourceFileHash,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        };
                    }
                }
            }
        }
    }
}