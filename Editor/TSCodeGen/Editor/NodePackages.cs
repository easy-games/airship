using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Airship.Editor {
    public struct PackageLockJson {
        [JsonProperty("packages")] 
        public Dictionary<string, PackageJson> Packages { get; set; }
    }
        
    public struct PackageJson {
        [JsonProperty("name")] 
        public string Name { get; set; }
        
        [JsonProperty("version")] 
        public string Version { get; set; }
        
        [JsonProperty("devDependencies")] 
        public Dictionary<string, string> DevDependencies { get; set; }
    }
    
    public class NodePackages {
        private static string _authToken;

        public static void LoadAuthToken() {
            _authToken = AuthConfig.instance.githubAccessToken;
        }

        public static PackageJson ReadPackageJson(string dir) {
            return JsonConvert.DeserializeObject<PackageJson>(File.ReadAllText(Path.Join(dir, "package.json")));
        }
        
        public static PackageLockJson ReadPackageLockJson(string dir) {
            return JsonConvert.DeserializeObject<PackageLockJson>(File.ReadAllText(Path.Join(dir, "package-lock.json")));
        }

        public static PackageJson GetPackageInfo(string dir, string packageName) {
            var path = Path.Join(dir, "node_modules", packageName);
            return ReadPackageJson(path);
        }

        private static Process RunCommand(string dir, string command) { 
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
#endif
            if (!procStartInfo.EnvironmentVariables.ContainsKey("EASY_AUTH_TOKEN"))
            {
                procStartInfo.EnvironmentVariables.Add("EASY_AUTH_TOKEN", _authToken);
            }

            var proc = new Process();
            proc.StartInfo = procStartInfo;
            if (!proc.StartInfo.Environment.ContainsKey("EASY_AUTH_TOKEN"))
            {
                proc.StartInfo.Environment["EASY_AUTH_TOKEN"] = _authToken;
            }
            UnityEngine.Debug.Log("using auth token: " + _authToken);

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
        
        public static bool RunNpmCommand(string dir, string command)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                UnityEngine.Debug.LogError("Missing Github Access Token! Add in Airship > Configuration");
                return false;
            }

            return RunCommand(dir, command).ExitCode == 0;
        }
    }
}