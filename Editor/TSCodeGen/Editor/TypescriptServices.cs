using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CsToTs.TypeScript;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    [Serializable]
    public class TypescriptCompilerWatchState {
        [SerializeField]
        public int processId;
        [SerializeField]
        public string directory;
        public Process CompilerProcess { get; private set; }

        public TypescriptCompilerWatchState(string directory) {
            this.directory = directory;
        }

        public bool IsActive => CompilerProcess is { HasExited: false };

        public bool StartWatchMode() {
            return ThreadPool.QueueUserWorkItem(delegate {
                this.CompilerProcess = TypescriptServices.RunCommand(this.directory, $"node ./node_modules/@easy-games/unity-ts/out/CLI/cli.js build --watch");
                this.processId = this.CompilerProcess.Id;
                TypeScriptCompilerRuntimeState.instance.Update();
            });
        }
    }
    
    [FilePath("Temp/TypescriptCompilerRuntimeState.tmp", FilePathAttribute.Location.ProjectFolder)]
    public class TypeScriptCompilerRuntimeState : ScriptableSingleton<TypeScriptCompilerRuntimeState> {
        [SerializeField] 
        internal List<TypescriptCompilerWatchState> watchStates = new();
        
        public int CompilerCount => watchStates.Count;
        
        internal void Update() {
            Save(true);
        }
    }
    
    /// <summary>
    /// Services relating to the TypeScript compiler in the editor ??
    /// </summary>
    [InitializeOnLoad]
    public static class TypescriptServices {
        static TypescriptServices() {
            if (Application.isPlaying) return;
            
            StopCompilerServices(true);

            if (EditorIntegrationsConfig.instance.automaticTypeScriptCompilation && !SessionState.GetBool("StartedTypescriptCompiler", false)) {
                SessionState.SetBool("StartedTypescriptCompiler", true);
                
                SetupProjects();
                StartCompilerServices();
            }
        }

        private static void SetupProjects() {
            CompileTypeScript(TypeScriptCompileFlags.Setup | TypeScriptCompileFlags.DisplayProgressBar);
        }
        
        [MenuItem("Airship/Full Script Rebuild")]
        internal static void FullRebuild()
        {
            CompileTypeScript(TypeScriptCompileFlags.FullClean);
        }

        [MenuItem("Airship/TypeScript/Start Compiler Services")]
        internal static void StartCompilerServices() {
            StopCompilers();
            
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories(includeFlags: TypescriptDirectorySearchFlags.NodeModules);
            foreach (var directory in typeScriptDirectories) {
                var watcher = new TypescriptCompilerWatchState(directory);
                if (watcher.StartWatchMode()) {
                    TypeScriptCompilerRuntimeState.instance.watchStates.Add(watcher);
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
            foreach (var compilerState in TypeScriptCompilerRuntimeState.instance.watchStates) {
                if (compilerState.processId == 0) continue;

                try {
                    var process = compilerState.CompilerProcess ?? Process.GetProcessById(compilerState.processId);
                    process.Kill();
                }
                catch {}
            }

            if (shouldRestart) { 
                Debug.LogWarning("Detected script reload - watch state for compiler(s) were restarted");
                
                foreach (var compilerState in TypeScriptCompilerRuntimeState.instance.watchStates) {
                    compilerState.StartWatchMode();
                }
            }
            else {
                TypeScriptCompilerRuntimeState.instance.watchStates.Clear();
            }

            TypeScriptCompilerRuntimeState.instance.Update();
        }

        private static bool _compiling;
        private static readonly GUIContent BuildButtonContent; 
        private static readonly GUIContent CompileInProgressContent;
        private static void CompileTypeScriptProject(string packageDir, TypeScriptCompileFlags compileFlags) {
            var shouldClean = (compileFlags & TypeScriptCompileFlags.FullClean) != 0;
            var skipInstalled = !shouldClean && (compileFlags & TypeScriptCompileFlags.Setup) != 0;

            if (!File.Exists(Path.Join(packageDir, "package.json"))) {
                return;
            }
            
            var packageInfo = NodePackages.ReadPackageJson(packageDir);

            var outPath = Path.Join(packageDir, "out");
            if (shouldClean && Directory.Exists(outPath))
            {
                Debug.Log("Deleting out folder..");
                Directory.Delete(outPath, true);
            }

            if (skipInstalled && Directory.Exists(Path.Join(packageDir, "node_modules"))) {
                return; // ??
            }

            try
            {
                _compiling = true;

                UpdateCompilerProgressBarText($"Installing node packages for {packageDir}...");
                var success = RunNpmInstall(packageDir);
                if (!success)
                {
                    Debug.LogWarning("Failed to install NPM dependencies");
                    _compiling = false;
                    return;
                }

                UpdateCompilerProgressBarText($"Running TypeScript compiler for {packageDir}...");
                var successfulBuild = RunNpmBuild(packageDir);
                _compiling = false;
                if (successfulBuild)
                {
                    Debug.Log($"<color=#77f777><b>Successfully built '{packageInfo.Name}'</b></color>");
                }
                else
                {
                    Debug.LogWarning($"<color=red><b>Failed to build'{packageInfo.Name}'</b></color>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        private static float progress = 0;
        private static void UpdateCompilerProgressBar(float progress, string text) {
            TypescriptServices.progress = progress;
            EditorUtility.DisplayProgressBar("Compiling TypeScript Projects", text, progress);
        }
        
        private static void UpdateCompilerProgressBarText(string text) {
            EditorUtility.DisplayProgressBar("Compiling TypeScript Projects", text, TypescriptServices.progress);
        }
        
        private static void CompileTypeScript(TypeScriptCompileFlags compileFlags = 0) {
            var displayProgressBar = (compileFlags & TypeScriptCompileFlags.DisplayProgressBar) != 0;
            var packages = GameConfig.Load().packages;
            
            Dictionary<string, string> localPackageTypescriptPaths = new();
            List<string> typescriptPaths = new();

            // @Easy/Core has the highest priority for internal dev
            var compilingCorePackage = false;
            
            // Fetch all 
            foreach (var package in packages)
            {
                // Compile local packages first
                if (!package.localSource) continue;
                var tsPath = TypeScriptDirFinder.FindTypeScriptDirectoryByPackage(package);
                if (tsPath == null) {
                    Debug.LogWarning($"{package.id} is declared as a local package, but has no TypeScript code?");
                    continue;
                }

                localPackageTypescriptPaths.Add(package.id, tsPath);
            }
            
            // Grab any non-package TS dirs
            var packageDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var packageDir in packageDirectories) {
                if (localPackageTypescriptPaths.ContainsValue(packageDir)) continue;
                typescriptPaths.Add(packageDir);
            }

            var totalCompileCount = localPackageTypescriptPaths.Count + typescriptPaths.Count;
            var compiled = 0;
            
            UpdateCompilerProgressBar(0f, $"Compiling {totalCompileCount} TypeScript projects...");
            
            // Force @Easy/Core to front
            // If core package exists, then we force it to be compiled first
            if (localPackageTypescriptPaths.ContainsKey("@Easy/Core")) {
                var corePkgDir = localPackageTypescriptPaths["@Easy/Core"];
                localPackageTypescriptPaths.Remove("@Easy/Core");
                
                compilingCorePackage = true;
                
                CompileTypeScriptProject(corePkgDir, compileFlags);
                compilingCorePackage = false;
                compiled += 1;
               
                UpdateCompilerProgressBar((float) compiled / totalCompileCount, $"Compiled @Easy/Core ({compiled} of {totalCompileCount})");
            }



            var compilingLocalPackage = false;
            // Compile each additional local package
            foreach (var packageDir in localPackageTypescriptPaths.Values) {
                compilingLocalPackage = true;
                CompileTypeScriptProject(packageDir, compileFlags);
                compilingLocalPackage = false;
                compiled += 1;
                
                UpdateCompilerProgressBar((float) compiled / totalCompileCount, $"Compiled {packageDir} ({compiled} of {totalCompileCount})");
            }

            var compilingTs = false;
            // Compile the non package TS dirs
            foreach (var packageDir in typescriptPaths) {
                CompileTypeScriptProject(packageDir, compileFlags); 
                compiled += 1;
   
                UpdateCompilerProgressBar((float) compiled / totalCompileCount, $"Compiled {packageDir} ({compiled} of {totalCompileCount})");
            }
            

            EditorUtility.ClearProgressBar();
            
        }

        private static bool RunNpmInstall(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "install");
        }

        private static bool RunNpmBuild(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "run build");
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
            AttachProcessOutputToUnityConsole(dir, proc);
            
            return proc;
        }


        private static void AttachProcessOutputToUnityConsole(string dir, Process proc) {
            Debug.Log($"Attach logs to process {proc.Id} for {dir}");
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            
            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.Log(TerminalFormatting.Linkify(dir, TerminalFormatting.TerminalToUnity(data.Data)));
            };
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };
        }
    }

    [Flags]
    public enum TypeScriptCompileFlags {
        FullClean = 1 << 0,
        Setup = 1 << 1,
        DisplayProgressBar = 1 << 2,
    }
}