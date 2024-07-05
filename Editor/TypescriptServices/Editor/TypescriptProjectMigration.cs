﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Editor.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal class VscodeWorkspace {
        internal class Folder {
            public string path;
        }

        public Folder[] folders;
        public VscodeSettings settings = new VscodeSettings();
        
        public override string ToString() {
            var resultingJson = JsonConvert.SerializeObject(this, new JsonSerializerSettings() {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver() {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
            return resultingJson;
        }
    }
    
    internal class VscodeSettings {
        [JsonProperty("files.exclude")]
        public Dictionary<string, bool> excludeFiles;

        [JsonProperty("editor.defaultFormatter")]
        public string defaultFormatter;
        
        [JsonProperty("editor.formatOnSave")]
        public bool? formatOnSave;
        
        [JsonProperty("[typescript]")] public VscodeSettings typescriptSettings;

        [JsonProperty("files.eol")] public string endOfLine;
        
        public override string ToString() {
            var resultingJson = JsonConvert.SerializeObject(this, new JsonSerializerSettings() {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver() {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
            return resultingJson;
        }
    }
    
    /// <summary>
    /// Temporary migration stuff for Airship project v1 to v2
    /// </summary>
    public class TypescriptProjectMigration {
        private static void MoveContents(string sourceDirectory, string targetDirectory) {
            Debug.Log($"MoveContents({sourceDirectory}, {targetDirectory})");

            if (!Directory.Exists(targetDirectory)) {
                Debug.Log($"mkdir {targetDirectory}");
                Directory.CreateDirectory(targetDirectory);
            }
            
            foreach (var fileName in Directory.EnumerateFiles(sourceDirectory)) {
                try {
                    File.Move(fileName, PosixPath.ToPosix(fileName.Replace(sourceDirectory, targetDirectory)));
                }
                catch (Exception e) {
                    Debug.LogError($"Could not move file {fileName}: {e.Message}");
                }
            }

            foreach (var folderFullName in Directory.EnumerateDirectories(sourceDirectory)) {
                var folderName = Path.GetFileName(folderFullName);
                if (folderName == "TS") {
                    Debug.Log("rm " + folderFullName);
                    Directory.Delete(targetDirectory + "/" + folderName);
                    continue; // skip Luau files - we're regenerating those!
                }
                
                if (!Directory.Exists(targetDirectory + "/" + folderName)) {
                    Debug.Log("mkdir " + folderFullName);
                    Directory.CreateDirectory(targetDirectory + "/" + folderName);
                }
                
                MoveContents(folderFullName, targetDirectory + "/" + folderName);
            }
        }

        private static string PackageLuauPathToEquivalentTypescriptPath(string path) {
            if (path.StartsWith("@")) {
                return "Assets/AirshipPackages/" + path.Replace(".lua", ".ts");
            }
            
            var packagePath = path.Replace("Assets/Bundles", "Assets/AirshipPackages").Replace("/Resources/TS", "");
            if (File.Exists(packagePath)) {
                return packagePath;
            }
            else {
                return packagePath.Replace(".lua", ".ts");
            }
        }
        
        private static string LuauPathToEquivalentTypescriptPath(string path) {
            var packagePath = path.Replace("Assets/Bundles", "Assets").Replace("/Resources/TS", "");
            if (File.Exists(packagePath)) {
                return packagePath;
            }
            else {
                return packagePath.Replace(".lua", ".ts");
            }
        }
        
        private static void MigratePackageDirectory(string directory) {
            foreach (var subdirectory in Directory.GetDirectories(directory)) {
                var scopedPath = PosixPath.GetRelativePath("Assets/Bundles", subdirectory);

                var targetPath = "Assets/AirshipPackages/" + scopedPath;
                
                // Create our target scope package directory if not exists
                if (!Directory.Exists(targetPath)) {
                    Directory.CreateDirectory(targetPath);
                }
                
                Debug.Log($"Path {subdirectory}, scoped to {scopedPath}");
                var packageName = scopedPath.Split("/")[1];
                
                // Migrate the code first
                var codeDirectory = PosixPath.Join(subdirectory, $"{packageName}~"); // E.g. @Easy/Core/Core~ - this was the old directory format for code
                var sourceDir = PosixPath.Join(codeDirectory, "src");
                
                if (Directory.Exists(sourceDir)) {
                    foreach (var folder in Directory.EnumerateDirectories(sourceDir)) {
                        var folderPath = PosixPath.ToPosix(folder);
                        var folderName = Path.GetFileName(folder);

                        var targetFolder = PosixPath.Join(targetPath, folderName);
                        Debug.Log($"Moved {folderPath} to {targetFolder} ({folderName})");
                        MoveContents(folderPath, targetFolder);
                    }
                }
                else {
                    Debug.LogWarning($"Could not find source dir {sourceDir}");
                }
            }
        }

        private static string[] excludeExtensions = {
            "asset",
            "meta",
            "mat",
            "prefab",
            "unity",
            "hdr",
            "aseditorinfo",
            "asbuildinfo",
            "confg",
            "wav",
            "png",
            "tga",
            "fbx",
            "anim",
            "pdf",
        };

        private static string[] excludeGlobs = {
            "FishNet.Config.XML",
            "AirshipPackages",
            "Typescript~",
        };
        
        public static void CreateVscodeSetings() {
            if (EditorUtility.DisplayDialog("Visual Studio Code Integration",
                    "Do you want to automatically configure your project's Assets folder for Visual Studio Code?", "Yes", "No")) {
                var excludeFiles = excludeExtensions.ToDictionary(exclusion => $"**/*.{exclusion}", _ => true);
                foreach (var glob in excludeGlobs) {
                    excludeFiles.Add(glob, true);
                }

                VscodeSettings settings = new VscodeSettings() {
                    typescriptSettings = new VscodeSettings() {
                        defaultFormatter = "esbenp.prettier-vscode",
                        formatOnSave = true,
                    },
                    excludeFiles = excludeFiles,
                    endOfLine = "\n",
                };

                if (!Directory.Exists("Assets/.vscode")) {
                    Directory.CreateDirectory("Assets/.vscode");
                }
            
                File.WriteAllText("Assets/.vscode/settings.json", settings.ToString());
            }
        }
        
        private static void MigrateTypescriptFiles() {
            var projectFolder = "Assets/Typescript~";
            
            // Ensure we have Typescript~ to work with
            if (!Directory.Exists(projectFolder)) {
                Debug.LogWarning("Could not upgrade project due to missing Typescript~ directory");
                return;
            }

            if (Directory.Exists("Assets/Bundles/Types~")) {
                Directory.Delete("Assets/Bundles/Types~", true);
            }

            // Create our packages directory
            if (!Directory.Exists("Assets/AirshipPackages")) {
                Directory.CreateDirectory("Assets/AirshipPackages");
            }
            
            // Migrate our packages across to the new folder
            if (Directory.Exists("Assets/Bundles")) {
                foreach (var directory in Directory.EnumerateDirectories("Assets/Bundles", "@*")) {
                    MigratePackageDirectory(directory);
                }
            }
                
            // copy user scripts across
            var src = Path.Join(projectFolder, "src").Replace("\\", "/");
            if (Directory.Exists(src)) {
                foreach (var folderPath in Directory.EnumerateDirectories(src)) {
                    var srcPath = folderPath.Replace("\\", "/");
                    var destinationPath = srcPath.Replace(src, "Assets");
                    Directory.Move(srcPath, destinationPath);
                }
            } else {
                Debug.LogWarning("Typescript~ does not contain 'src' directory - skipping step");
            }

            // Generate tsconfig.json
            if (!File.Exists("Assets/tsconfig.json")) {
                var paths = new Dictionary<string, string[]>();
                paths.Add("@*", new [] { "AirshipPackages/@*" }); // @Easy/Core should be AirshipPackages/@Easy/Core (as an example)
                
                var templateConfig = new TypescriptConfig() {
                    compilerOptions = new TypescriptConfig.CompilerOptions() {
                        outDir = "Typescript~/out",
                        baseUrl = ".",
                        paths = paths,
                        typeRoots = new[] { "Typescript~/node_modules/@easy-games" },
                        plugins = new [] {
                            new TypescriptPluginConfig() {
                                transform = "@easy-games/unity-flamework-transformer"
                            }
                        },
                    },
                    airship = new TypescriptConfig.AirshipConfig() {
                        ProjectType = TypescriptConfig.ProjectType.Game,
                        PackageFolderPath = "Typescript~",
                        RuntimeFolderPath = "AirshipPackages/@Easy/Core/Shared/Runtime",
                    },
                    include = new[] {
                        "**/*.ts",
                        "**/*.tsx",
                    },
                    exclude = new[] {
                        "Typescript~/node_modules"
                    },
                };
                
                File.WriteAllText("Assets/tsconfig.json", templateConfig.ToString());
            }
            else {
                Debug.LogWarning("tsconfig.json exists - skipping");
            }
            
            // It's time to refresh
            TypescriptProjectsService.ReloadProject();
            TypescriptProjectsService.CheckTypescriptProject();
            TypescriptCompilationService.FullRebuild();

            FixScriptBindings();
            
            CreateVscodeSetings();
            TypescriptCompilationService.StartCompilerServices();
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public static void MigrateScriptBinding(AirshipComponent binding) {
            var path = binding.m_fileFullPath;

            if (!path.StartsWith("Assets/Bundles") && !path.StartsWith("@")) {
                return;
            }
            
            string newPath;

            if (path.StartsWith("Assets/Bundles/@") || path.StartsWith("@")) {
                // Is pkg
                newPath = PackageLuauPathToEquivalentTypescriptPath(binding.m_fileFullPath);
            }
            else {

                newPath = LuauPathToEquivalentTypescriptPath(binding.m_fileFullPath);
            }
                
            binding.SetScriptFromPath(newPath, LuauContext.Game);
            Debug.Log($"Convert path {path} -> {newPath}");
            EditorUtility.SetDirty(binding);
        }
#pragma warning restore CS0618 // Type or member is obsolete
        
        // [MenuItem("Airship/Project/Repair Script Bindings", validate = true)]
        public static bool CanFixScriptBindings() {
            return TypescriptProjectsService.Project != null;
        }
        
        // [MenuItem("Airship/Project/Repair Script Bindings", priority = 20)]
        public static void FixScriptBindings() {
            string[] bindingGuids = AssetDatabase.FindAssets("t:" + nameof(AirshipComponent));
            foreach (var bindingGuid in bindingGuids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(bindingGuid);
                var binding = AssetDatabase.LoadAssetAtPath<AirshipComponent>(assetPath);
                MigrateScriptBinding(binding);
            }
            
            var scriptBindings = Resources.FindObjectsOfTypeAll<AirshipComponent>();
            foreach (var binding in scriptBindings) {
                MigrateScriptBinding(binding);
            }
        }
        
        [MenuItem("Airship/Project/Migrate to Project V2", validate = true)]
        public static bool CanMigrateProject() {
            return TypescriptProjectsService.Project == null;
        }
        
        [MenuItem("Airship/Misc/Migrate to Project V2")]
        public static void MigrateProject() {
            if (EditorUtility.DisplayDialog("Upgrade to the new project format", "Are you sure you want to upgrade your project?\n\nThis will migrate your code and references to the code - packages also may need to be updated/redownloaded.",
                    "Yes", "No")) {
                Debug.Log("Migrating to project version 2...");
                if (!TypescriptConfig.ExistsInDirectory("Assets")) {
                    // There's no root level tsconfig, should be an old project
                    MigrateTypescriptFiles();
                }
                else {
                
                    EditorUtility.DisplayDialog("Upgrade", "Already using the new project format!", "OK");
                }
            }
        }


    }
}