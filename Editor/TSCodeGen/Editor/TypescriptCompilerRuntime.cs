using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CsToTs.TypeScript;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    public class TypeScriptCompilerState {
        public readonly string Directory;
        public bool IsRunning { get; private set; } = false;
        public Process CompilerProcess { get; private set; }

        public TypeScriptCompilerState(string directory) {
            this.Directory = directory;
        }

        public bool StartWatchMode() {
            return ThreadPool.QueueUserWorkItem(delegate {
                this.IsRunning = true;
                this.CompilerProcess = TypescriptCompilerRuntime.RunCommand(this.Directory, $"node ./node_modules/@easy-games/unity-ts/out/CLI/cli.js build --watch");
                this.IsRunning = false;
            });
        }
    }

    public static class TypeScriptCompilerRuntimeState {
        internal static Dictionary<string, TypeScriptCompilerState> compilers = new();
    }
    
    [InitializeOnLoad]
    public class TypescriptCompilerRuntime {
        static TypescriptCompilerRuntime() {
            // test
            // if (SessionState.GetBool("TypescriptCompilersRunning", false)) return;
            //
            // SessionState.SetBool("TypeScriptCompilersRunning", true);
            // foreach (var compiler in TypeScriptCompilerRuntimeState.compilers) {
            //     compiler.Value.CompilerProcess?.Kill();
            // }
            //
            // if (EditorIntegrationsConfig.instance.automaticTypeScriptCompilation) {
            //     StartCompilers();
            // }
            
            //testing lmfao
        }

        [MenuItem("Airship/TypeScript/Run Compiler")]
        internal static void StartCompilers() {
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var directory in typeScriptDirectories) {
                var watcher = new TypeScriptCompilerState(directory);
                if (watcher.StartWatchMode()) {
                    // TypeScriptCompilerRuntimeState.compilers.Add(directory, watcher);
                }
            }
        }

        [MenuItem("Airship/TypeScript/Stop Compiler")]
        internal static void StopCompilers() {
            foreach (var compiler in TypeScriptCompilerRuntimeState.compilers.Values) {
                Debug.Log($"Attempt to kill compiler {compiler.Directory}: {compiler.IsRunning}, {compiler.CompilerProcess != null}");
                compiler.CompilerProcess.Kill();
            }
        }

        internal static Process RunCommand(string dir, string command, bool displayOutput = true) { 
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && {command}\"";

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
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/K {command}")
            {
                RedirectStandardOutput = displayOutput,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
                Environment = {
                    { "FORCE_COLOR", "0" }
                }
            };
#endif

            var proc = new Process();
            proc.StartInfo = procStartInfo;

            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.Log(TerminalFormatting.TerminalToUnity(data.Data));
            };
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            // proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"Exit code is: {proc.ExitCode}");
            }
            
            return proc;
        }
    }
}