using System;
using System.Diagnostics;
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

        public static string FindExecutableOnPath(string executable) {
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

        public static string FindNodeBinPath() {
#if UNITY_EDITOR_WIN
            // Windows is ensured to be in PATH
            return FindExecutableOnPath("node");
#else
            // We can check via PATH first -
            var pathExecutable = FindExecutableOnPath("node");
            if (pathExecutable != null) {
                return pathExecutable;
            }
                
            Debug.LogWarning("Node is not on your PATH");
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