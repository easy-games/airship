using System;
using System.Collections.Generic;
using System.IO;
using Editor.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
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
                if (folderName == "TS") continue; // skip Luau files - we're regenerating those!
                
                if (!Directory.Exists(targetDirectory + "/" + folderName)) {
                    Directory.CreateDirectory(targetDirectory + "/" + folderName);
                }
                
                MoveContents(folderFullName, targetDirectory + "/" + folderName);
            }
        }
        
        private static void MigratePackageDirectory(string directory) {
            foreach (var subdirectory in Directory.GetDirectories(directory)) {
                var scopedPath = PosixPath.GetRelativePath("Assets/Bundles", subdirectory);

                var sourcePath = "Assets/Bundles/" + scopedPath;
                var targetPath = "Assets/AirshipPackages/" + scopedPath;
                
                // Create our target scope package directory if not exists
                if (!Directory.Exists(targetPath)) {
                    Directory.CreateDirectory(targetPath);
                }
                
                Debug.Log($"Path {subdirectory}, scoped to {scopedPath}");
                var packageName = scopedPath.Split("/")[1];

                var migrated = new HashSet<string>();
                // Migrate the code first
                var codeDirectory = PosixPath.Join(subdirectory, $"{packageName}~"); // E.g. @Easy/Core/Core~ - this was the old directory format for code
                var sourceDir = PosixPath.Join(codeDirectory, "src");
                
                if (Directory.Exists(sourceDir)) {
                    migrated.Add(codeDirectory);

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
                
                // Migrate other assets
                // foreach (var directoryFullPath in Directory.EnumerateDirectories(subdirectory)) {
                //     if (directoryFullPath == codeDirectory || directoryFullPath.EndsWith("~")) continue;
                //     migrated.Add(directoryFullPath);
                //     
                //     
                //     // Debug.Log($"Would migrate {directoryFullPath}");
                //     MoveContents(directoryFullPath, targetPath);
                // }
            }
        }
        
        private static void MigrateTypescriptFiles() {
            var projectFolder = "Assets/Typescript~";
            Debug.Log("Testing lmfao");
            
            // Ensure we have Typescript~ to work with
            if (!Directory.Exists(projectFolder)) {
                Debug.LogWarning("Could not upgrade project due to missing Typescript~ directory");
                return;
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
            }
            else {
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
                        typeRoots = new[] { "Typescript~/node_modules/@easy-games", "Typescript~/types" },
                    },
                    airship = new TypescriptConfig.AirshipConfig() {
                        ProjectType = TypescriptConfig.ProjectType.Game,
                    },
                    include = new[] {
                        "**/*.ts",
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
            AssetDatabase.StartAssetEditing();
            foreach (var file in Directory.EnumerateFiles("Assets", "*.ts", SearchOption.AllDirectories)) {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
            }
            foreach (var file in Directory.EnumerateFiles("Assets", "tsconfig.json", SearchOption.AllDirectories)) {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
            }
            AssetDatabase.StopAssetEditing();
            
            // TypescriptCompilationService.FullRebuild();
        }
        
        [MenuItem("Airship/Migrate to Project V2...", priority = 10)]
        public static void MigrateProject() {
            
            Debug.Log("Test");
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