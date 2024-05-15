using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.Editor;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Editor {
    [ScriptedImporter(1, "ts")]
    public class TypescriptImporter : LuauImporter {
        private const string IconOk = "Packages/gg.easy.airship/Editor/TypescriptAssetOk.png";
        private const string IconEmpty = "Packages/gg.easy.airship/Editor/TypescriptAssetUncompiled.png";
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
                
                return TypescriptConfig.FindInDirectory(directory, out _projectConfig, file) ? _projectConfig : null;
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
        
        public override void OnImportAsset(AssetImportContext ctx) {
            if (ctx.assetPath.EndsWith(".d.ts")) {
                var airshipScript = ScriptableObject.CreateInstance<Luau.DeclarationFile>();
                var source = File.ReadAllText(ctx.assetPath);
                airshipScript.ambient = !source.Contains("export ");

                var declarationForFile = ctx.assetPath.Replace(".d.ts", ".lua").Replace("index.lua", "init.lua");
                if (File.Exists(declarationForFile)) {
                    airshipScript.isLuauDeclaration = true;
                    airshipScript.scriptPath = declarationForFile;
                }
                
                var ext = Path.GetExtension(ctx.assetPath);
                var fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconEmpty);
                ctx.AddObjectToAsset(fileName, airshipScript, icon);
                ctx.SetMainObject(airshipScript);
            }
            else {
                var airshipScript = ScriptableObject.CreateInstance<Luau.BinaryFile>();
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
                        var (_, result) = CompileLuauAsset(ctx, airshipScript, outPath);
                        if (!result.Value.Compiled) {
                            typescriptIconPath = IconFail;
                        }
                    }
                    else {
                        Debug.LogWarning($"Could not find Luau file at path {outPath}");
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
}