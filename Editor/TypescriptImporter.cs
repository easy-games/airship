using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.Editor;
using Code.Luau;
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

    public enum ScriptType {
        [InspectorName("Game Logic (Components & Scripts)")]
        GameScript,
        [InspectorName("Rendering (URP Render Pass)")]
        RenderPassScript,
    }
    
    [ScriptedImporter(1, "ts")]
    public class TypescriptImporter : LuauImporter {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAsset.png";
        private const string IconRenderOk = "Packages/gg.easy.airship/Editor/RenderScriptAsset.png";
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

        public ScriptType ScriptType = ScriptType.GameScript;
        
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

                AirshipScriptable scriptableAsset;
                switch (ScriptType) {
                    case ScriptType.GameScript: {
                        var script = ScriptableObject.CreateInstance<Luau.AirshipScript>();
                        script.scriptLanguage = AirshipScriptLanguage.Typescript;
                        script.assetPath = ctx.assetPath;
                    
                        scriptableAsset = script;
                        break;
                    }
                    case ScriptType.RenderPassScript: {
                        var script = ScriptableObject.CreateInstance<AirshipRenderPassScript>();
                        scriptableAsset = script;
                        break;
                    }
                    default:
                        return;
                }
               

                var project = TypescriptProjectsService.Project;
                var ext = Path.GetExtension(ctx.assetPath);
                var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
            
                var typescriptIconPath = IconEmpty;
                if (project != null && Directory.Exists(ProjectConfig.Directory)) {
                    var outPath = project.GetOutputPath(ctx.assetPath);
                    if (File.Exists(outPath)) {
                        typescriptIconPath = IconOk;
                        hasCompiled = true;
                        var (_, result) = CompileLuauAsset(ctx, scriptableAsset, outPath);
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

                switch (scriptableAsset) {
                    case AirshipScript gameScript: {
                        if (gameScript.m_metadata?.displayIcon != null && hasCompiled) {
                            icon = gameScript.m_metadata.displayIcon;
                        }
                        else {
                            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(typescriptIconPath);
                        }


                        gameScript.typescriptWasCompiled = hasCompiled;
                        ctx.AddObjectToAsset(fileName, gameScript, icon);
                        ctx.SetMainObject(gameScript);
                        break;
                    }
                    case AirshipRenderPassScript renderPassScript:
                        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(hasCompiled ? IconRenderOk : typescriptIconPath);
                        ctx.AddObjectToAsset(fileName, renderPassScript, icon);
                        ctx.SetMainObject(renderPassScript);
                        break;
                }
            }
        }
        
    }
}