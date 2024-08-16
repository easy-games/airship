using System;
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
    }
}