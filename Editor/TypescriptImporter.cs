using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.Editor;
using Editor.Util;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Editor {
    [ScriptedImporter(1, "ts")]
    public class TypescriptImporter : LuauImporter {
        public bool someTest = true;
        
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
                
                var directory = Path.GetDirectoryName(EditorIntegrationsConfig.instance.typescriptProjectConfig);
                var file = Path.GetFileName(EditorIntegrationsConfig.instance.typescriptProjectConfig);
                
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

        private const string CorePath = "Assets/AirshipPackages/@Easy/Core"; 
        // private string GetCorePrecompiledPath(string inputFilePath) {
        //     var tsConfig = TypescriptProjectsService.Project.TsConfig;
        //     
        //     foreach (var rootDir in tsConfig.RootDirs) {
        //         if (!inputFilePath.StartsWith(rootDir)) continue;
        //         var output = inputFilePath.Replace(CorePath, Path.Join(CorePath, "__PrecompiledLuau~")).Replace("\\", "/");
        //         return FileExtensions.Transform(output, FileExtensions.Typescript, FileExtensions.Lua);
        //     }
        //
        //     return null;
        // }
        
        public override void OnImportAsset(AssetImportContext ctx) {
            var isCoreAsset = ctx.assetPath.StartsWith(CorePath);
            
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
                    string outPath;
                    
                    var corePath = Path.Join(project.PrecompiledLuauDirectory, "@Easy", "Core").Replace("\\", "/");
                    if (isCoreAsset && Directory.Exists(corePath)) {
                        outPath = project.GetPrecompiledOutputPath(ctx.assetPath);
                        airshipScript.scriptLanguage = AirshipScriptLanguage.PrecompiledLuau;
                    }
                    else {
                        outPath = project.GetOutputPath(ctx.assetPath);
                    }

                    airshipScript.compiledLuauPath = outPath;
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
                
                
                ctx.AddObjectToAsset(fileName, airshipScript, icon);
                ctx.SetMainObject(airshipScript);
            }
            
 
        }
    }
}