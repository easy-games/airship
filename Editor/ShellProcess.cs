using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Debug = UnityEngine.Debug;

namespace Editor {
    /// <summary>
    /// Platform-dependent shell commands
    /// </summary>
    public class ShellCommand {
        public string Arguments { get; private set; }
        private ShellCommand(string windows, string posix) {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            Arguments = posix;
#else
            Arguments = windows;
#endif
        }
        
        public override string ToString() {
            return Arguments;
        }

        public static implicit operator ShellCommand((string windows, string posix) command) {
            return new ShellCommand(command.windows, command.posix);
        }
        
        public static implicit operator ShellCommand(string command) {
            return new ShellCommand(command, command);
        }
    }

    public class ShellProcess {
        public static ProcessStartInfo GetStartInfoForCommand(string workingDirectory, string executable, string arguments) {
            var procStartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = arguments,
            };
            return procStartInfo;
        }

        internal static string FindExecutableOnPath(string executable) {
#if UNITY_EDITOR_WIN
            var proc = new Process();
            var startInfo = GetShellStartInfoForCommand($"where {executable}", Environment.CurrentDirectory);
            startInfo.RedirectStandardOutput = true;
            proc.StartInfo = startInfo;
            proc.Start();
            var res = proc.StandardOutput.ReadToEnd().Split("\r\n")[0];
            return res;
#else
            var proc = new Process();
            var startInfo = GetShellStartInfoForCommand($"command -v {executable}", Environment.CurrentDirectory);
            startInfo.RedirectStandardOutput = true;
            proc.StartInfo = startInfo;
            proc.Start();
            var res = proc.StandardOutput.ReadToEnd().Split("\n")[0];
            return res;
#endif
        }

        public static string[] FindNodeBinaryPaths()
        {
            HashSet<string> nodeInstallPaths = new();

            var nodeEnvironmentPath = FindExecutableOnPath("node");
            if (nodeEnvironmentPath != "")
            {
                nodeInstallPaths.Add(Path.GetDirectoryName(nodeEnvironmentPath));
            }
            
#if !UNITY_EDITOR_WIN
            // Find NVM installs for unix
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nvm = Path.Join(home, ".nvm/versions/node");
            if (Directory.Exists(nvm))
            {
                foreach (var directory in Directory.GetDirectories(nvm))
                {
                    var binNodePath = Path.Join(directory, "bin/node");
                    if (!File.Exists(binNodePath)) continue;
                    
                    nodeInstallPaths.Add(Path.Join(directory, "bin"));
                }
            }

            if (File.Exists("/usr/bin/node")) nodeInstallPaths.Add("/usr/bin");
            if (File.Exists("/usr/local/bin/node")) nodeInstallPaths.Add("/usr/local/bin");
#endif
            
            return nodeInstallPaths.ToArray();
        }

        public static string FindNodeBinPath() {
#if UNITY_EDITOR_WIN
            // Windows is ensured to be in PATH
            return FindExecutableOnPath("node");
#else
            // We can check via PATH first -
            var pathExecutable = FindExecutableOnPath("node");
            if (pathExecutable != "") {
                return pathExecutable;
            }

            var otherPaths = FindNodeBinaryPaths();
            if (otherPaths.Length > 0)
            {
                return otherPaths[0];
            }
                
            Debug.LogWarning("Could not find the path of Node on your system");
            return null;
#endif
        }
        
        /// <summary>
        /// Gets the operating system specific ProcessStartInfo for the given command
        /// </summary>
        /// <param name="command">The command</param>
        /// <param name="dir">The directory to execute the command in</param>
        /// <returns>The process start info</returns>
        public static ProcessStartInfo GetShellStartInfoForCommand(ShellCommand command, string dir) {
#if UNITY_EDITOR_OSX
            var procStartInfo = GetStartInfoForCommand(dir, "/bin/zsh", $"-l -c \"{command.Arguments}\"");
            return procStartInfo;
#elif UNITY_EDITOR_LINUX
            var procStartInfo = GetStartInfoForCommand(dir, "/bin/bash", $"-lc \"{command.Arguments}\"");
            return procStartInfo;
#else
            var procStartInfo = GetStartInfoForCommand(dir, "cmd.exe", $"/C {command.Arguments}");
            return procStartInfo;
#endif
        }
    }
}