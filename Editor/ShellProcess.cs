using System.Diagnostics;

namespace Editor {
    /// <summary>
    /// Platform-dependent shell commands
    /// </summary>
    public class ShellCommand {
        public string CommandString { get; private set; }

        private ShellCommand(string windows, string posix) {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            CommandString = posix;
#else
            CommandString = windows;
#endif
        }
        
        public override string ToString() {
            return CommandString;
        }

        public static implicit operator ShellCommand((string windows, string posix) command) {
            return new ShellCommand(command.windows, command.posix);
        }
        
        public static implicit operator ShellCommand(string command) {
            return new ShellCommand(command, command);
        }
    }

    public class ShellProcess {
        /// <summary>
        /// Gets the operating system specific ProcessStartInfo for the given command
        /// </summary>
        /// <param name="command">The command</param>
        /// <param name="dir">The directory to execute the command in</param>
        /// <returns>The process start info</returns>
        public static ProcessStartInfo GetStartInfoForCommand(ShellCommand command, string dir) {
#if UNITY_EDITOR_OSX
            command = $"-l -c \"{command}\"";
            var procStartInfo = new ProcessStartInfo( "/bin/zsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = command.CommandString,
            };
            return procStartInfo;
#elif UNITY_EDITOR_LINUX
            // Linux uses bash
            var procStartInfo = new ProcessStartInfo( "/bin/bash")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = command.CommandString,
            };
#else
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/K {command}") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
            return procStartInfo;
#endif
        }
    }
}