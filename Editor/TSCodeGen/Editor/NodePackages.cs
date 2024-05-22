using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        [CanBeNull]
        public PackageJson GetDependencyInfo(string package) {
            return NodePackages.GetPackageInfo(this.Directory, package);
        }

        public bool IsLocalInstall(string package) {
            return (DevDependencies != null && DevDependencies.ContainsKey(package) && DevDependencies[package].StartsWith("file:")) 
                   || (Dependencies != null && Dependencies.ContainsKey(package) && Dependencies[package].StartsWith("file:"));
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
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && npm {command}\"";
            // command = "-c \"whoami && ls /usr/local/bin\"";
            // command = "/usr/local/bin";
            // command = "-c \"alias node=\"/usr/local/bin/node\" && /usr/local/bin/npm run build\"";
            // command = "-c \"scripts/build.sh\"";
            var procStartInfo = new ProcessStartInfo( "/bin/zsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // RedirectStandardInput = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = command,
            };
#else
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/K npm {command}")
            {
                RedirectStandardOutput = displayOutput,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
#endif

            var proc = new Process();
            proc.StartInfo = procStartInfo;

            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.Log(data.Data);
            };
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"Exit code is: {proc.ExitCode}");
            }
            
            return proc;
        }

        public static List<string> GetCommandOutput(string dir, string command) {
            var items = new List<string>();
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && npm {command}\"";
            var procStartInfo = new ProcessStartInfo( "/bin/zsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // RedirectStandardInput = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = command,
            };
#else
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/C npm {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
#endif
            var proc = new Process();
            proc.StartInfo = procStartInfo;
            
 
            
            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                items.Add(data.Data);
            };
            
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();
            return items;
        }
        
        public static bool RunNpmCommand(string dir, string command, bool displayOutput = true)
        {
            return RunCommand(dir, command, displayOutput).ExitCode == 0;
        }
    }
}