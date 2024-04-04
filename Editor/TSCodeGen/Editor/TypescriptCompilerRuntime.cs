using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CsToTs.TypeScript;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    [Serializable]
    public class TypeScriptCompilerState {
        [SerializeField]
        public int processId;
        [SerializeField]
        public string directory;
        [SerializeField]
        public bool isRunning = false;
        public Process CompilerProcess { get; private set; }

        public TypeScriptCompilerState(string directory) {
            this.directory = directory;
        }

        public bool StartWatchMode() {
            return ThreadPool.QueueUserWorkItem(delegate {
                this.isRunning = true;
                this.CompilerProcess = TypescriptCompilerRuntime.RunCommand(this.directory, $"node ./node_modules/@easy-games/unity-ts/out/CLI/cli.js build --watch");
                this.processId = this.CompilerProcess.Id;
                TypeScriptCompilerRuntimeState.instance.Update();
            });
        }
    }
    
    [FilePath("Temp/TypescriptCompilerRuntimeState.tmp", FilePathAttribute.Location.ProjectFolder)]
    public class TypeScriptCompilerRuntimeState : ScriptableSingleton<TypeScriptCompilerRuntimeState> {
        [SerializeField] public List<TypeScriptCompilerState> compilerStates = new();

        public void Update() {
            Save(true);
        }
    }
    
    [InitializeOnLoad]
    public class TypescriptCompilerRuntime {
        static TypescriptCompilerRuntime() {
            StopCompilerServices(true);
        }

        [MenuItem("Airship/TypeScript/Start Compiler Services")]
        internal static void StartCompilerServices() {
            StopCompilers();
            
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var directory in typeScriptDirectories) {
                var watcher = new TypeScriptCompilerState(directory);
                if (watcher.StartWatchMode()) {
                    TypeScriptCompilerRuntimeState.instance.compilerStates.Add(watcher);
                    TypeScriptCompilerRuntimeState.instance.Update();
                }
                else {
                    Debug.LogWarning($"Could not start compiler for {directory}");
                }
            }
        }

        [MenuItem("Airship/TypeScript/Stop Compiler Services")]
        internal static void StopCompilers() {
            StopCompilerServices();
        }
        
        private static void StopCompilerServices(bool shouldRestart = false) {
            foreach (var compilerState in TypeScriptCompilerRuntimeState.instance.compilerStates) {
                if (compilerState.processId == 0) continue;

                try {
                    var process = compilerState.CompilerProcess ?? Process.GetProcessById(compilerState.processId);
                    process.Kill();
                }
                catch {}
            }

            if (shouldRestart) { 
                Debug.LogWarning("Detected script reload - watch state for compiler(s) were restarted");
                
                foreach (var compilerState in TypeScriptCompilerRuntimeState.instance.compilerStates) {
                    compilerState.StartWatchMode();
                }
            }
            else {
                TypeScriptCompilerRuntimeState.instance.compilerStates.Clear();
            }

            TypeScriptCompilerRuntimeState.instance.Update();
        }

        internal static Process RunCommand(string dir, string command, bool displayOutput = true) { 
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && {command}\"";

            var procStartInfo = new ProcessStartInfo( "/bin/zsh")
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
#else
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/C {command}")
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
            
            proc.Start();
            AttachProcessOutputToUnityConsole(proc);
            
            return proc;
        }


        private static void AttachProcessOutputToUnityConsole(Process proc) {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            
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
        }
    }
}