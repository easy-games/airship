﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Editor;
using Editor.Util;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    public struct PackageLockJson {
        [JsonProperty("packages")] 
        public Dictionary<string, PackageJson> Packages { get; set; }
    }
        
    public class PackageJson {
        [JsonProperty("name")] 
        public string Name { get; set; }
        
        [JsonProperty("version")] 
        public string Version { get; set; }
        
        [JsonProperty("scripts")] 
        public Dictionary<string, string> Scripts { get; set; }
        
        [JsonProperty("dependencies")] 
        public Dictionary<string, string> Dependencies { get; set; }
        
        [JsonProperty("devDependencies")] 
        public Dictionary<string, string> DevDependencies { get; set; }

        [JsonIgnore] public string Directory { get; internal set; }
        [JsonIgnore] public string FilePath { get; internal set; }

        [CanBeNull]
        public PackageJson GetDependencyInfo(string package) {
            return NodePackages.GetPackageInfo(this.Directory, package);
        }

        public bool IsPackageInstalled(string packageName) {
            if (!HasDependency(packageName)) return false;
            
            var nodeModules = Path.Join(Directory, "node_modules");
            var packagePath = PosixPath.Join(nodeModules, packageName);
            return File.Exists(PosixPath.Join(packagePath, "package.json"));
        }

        internal bool InstallDependency(string packageName, string version = "latest", bool dev = false) {
            return NodePackages.RunNpmCommand(Directory, dev ? $"install --save-dev {packageName}@{version}" : $"install {packageName}@{version}");
        }

        internal bool UninstallDependency(string packageName) {
            return NodePackages.RunNpmCommand(Directory, $"uninstall {packageName}");
        }
        
        public bool HasDependency(string package) {
            return (DevDependencies != null && DevDependencies.ContainsKey(package)) 
                   || (Dependencies != null && Dependencies.ContainsKey(package));
        }
        
        public bool IsLocalDependency(string package) {
            return (DevDependencies != null && DevDependencies.ContainsKey(package) && DevDependencies[package].StartsWith("file:")) 
                   || (Dependencies != null && Dependencies.ContainsKey(package) && Dependencies[package].StartsWith("file:"));
        }

        public bool IsGitDependency(string package) {
            return (DevDependencies != null && DevDependencies.ContainsKey(package) && DevDependencies[package].StartsWith("github:")) 
                   || (Dependencies != null && Dependencies.ContainsKey(package) && Dependencies[package].StartsWith("github:"));
        }

        public string GetDependencyString(string package) {
            if (Dependencies != null && Dependencies.TryGetValue(package, out var dependency)) {
                return dependency;
            } else if (DevDependencies != null && DevDependencies.TryGetValue(package, out var devDependency)) {
                return devDependency;
            }

            return null;
        }
        
        internal void Modify() {
            File.WriteAllText(FilePath, ToString());
        }
        
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
    
    public class NodePackages {
        public static void LoadAuthToken() {

        }
        
        [CanBeNull]
        public static PackageJson ReadPackageJson(string dir) {
            var file = Path.Join(dir, "package.json");
            if (!File.Exists(file)) return null;
            var packageJson = JsonConvert.DeserializeObject<PackageJson>(File.ReadAllText(file));
            packageJson.Directory = dir;
            packageJson.FilePath = file;
            return packageJson;
        }

        public static bool FindPackageJson(string dir, out PackageJson packageJson) {
            var file = Path.Join(dir, "package.json");
            if (!File.Exists(file)) {
                packageJson = null;
                return false;
            }
            
            packageJson = JsonConvert.DeserializeObject<PackageJson>(File.ReadAllText(file));
            packageJson.Directory = dir;
            packageJson.FilePath = file;
            return true;
        }
        
        public static PackageLockJson ReadPackageLockJson(string dir) {
            return JsonConvert.DeserializeObject<PackageLockJson>(File.ReadAllText(Path.Join(dir, "package-lock.json")));
        }
        
        [CanBeNull]
        public static PackageJson GetPackageInfo(string dir, string packageName) {
            var path = Path.Join(dir, "node_modules", packageName);
            if (Directory.Exists(path)) {
                return ReadPackageJson(path);
            }
            else {
                return null;
            }
        }

        public static Process RunCommand(string dir, string command, bool displayOutput = true) { 
            var procStartInfo = ShellProcess.GetShellStartInfoForCommand($"npm {command}", dir);
            var proc = new Process();
            proc.StartInfo = procStartInfo;

            if (displayOutput) {
                proc.OutputDataReceived += (_, data) =>
                {
                    if (data.Data == null) return;
                    UnityEngine.Debug.Log(data.Data);
                };
                
            }
            
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };
            
            proc.Start();
            
            if (displayOutput) {
                proc.BeginOutputReadLine();
            }
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"Exit code is: {proc.ExitCode}");
            }
            
            return proc;
        }

        public static bool GetCommandOutput(string dir, string command, out List<string> output) {
            var items = new List<string>();

            ShellCommand shellCommand = $"npm {command}";
            var procStartInfo = ShellProcess.GetShellStartInfoForCommand(shellCommand, dir);
            var proc = new Process();
            proc.StartInfo = procStartInfo;



            proc.OutputDataReceived += (_, data) => {
                if (data.Data == null) return;
                items.Add(data.Data);
            };

            proc.ErrorDataReceived += (_, data) => {
                if (data.Data == null) return;
                Debug.LogWarning(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            output = items;
            return items.Count > 0;
        }
        
        public static bool RunNpmCommand(string dir, string command, bool displayOutput = true)
        {
            return RunCommand(dir, command, displayOutput).ExitCode == 0;
        }
    }
}