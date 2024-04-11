using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Code.Bootstrap;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    internal enum CompilationState {
        Inactive,
        IsStandby,
        IsCompiling,
        HasErrors,
    }
    
    [Serializable]
    internal class TypescriptCompilerWatchState {
        [SerializeField]
        public int processId;
        [SerializeField]
        public string directory;
        internal CompilationState compilationState = CompilationState.Inactive;

        public TypescriptProject project {
            get {
                return new TypescriptProject(directory);
            }
        }

        public Process CompilerProcess { get; private set; }
        public bool IsActive => CompilerProcess is { HasExited: false };
        public bool IsCompiling => compilationState == CompilationState.IsCompiling;
        public bool HasErrors => compilationState == CompilationState.HasErrors;
        public int ErrorCount { get; internal set; }
        
        public TypescriptCompilerWatchState(string directory) {
            this.directory = directory;
        }

        public bool Watch() {
            return ThreadPool.QueueUserWorkItem(delegate {
                compilationState = CompilationState.IsCompiling;
                var watchArgs = EditorIntegrationsConfig.instance.TypeScriptWatchArgs;
                CompilerProcess = TypescriptCompilationService.RunNodeCommand(this.directory, $"{EditorIntegrationsConfig.instance.TypeScriptLocation} {string.Join(" ", watchArgs)}");
                TypescriptCompilationService.AttachWatchOutputToUnityConsole(this, CompilerProcess);
                processId = this.CompilerProcess.Id;
                TypescriptCompilationServicesState.instance.Update();
            });
        }
    }

    [FilePath("Temp/TypeScriptCompilationServicesState", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptCompilationServicesState : ScriptableSingleton<TypescriptCompilationServicesState> {
        [SerializeField] 
        internal List<TypescriptCompilerWatchState> watchStates = new();
        [SerializeField] 
        internal List<TypescriptProject> projects = new ();
        
        public int CompilerCount => watchStates.Count(compiler => compiler.IsActive);

        [CanBeNull]
        public TypescriptCompilerWatchState GetWatchStateForDirectory(string directory) {
            return watchStates.Find(f => f.directory == directory);
        }

        public TypescriptCompilerWatchState GetWatchStateForProject(TypescriptProject project) {
            return watchStates.Find(f => f.project == project);
        }
        
        internal void Update() {
            Save(true);
        }
    }
    
    /// <summary>
    /// Services relating to the TypeScript compiler in the editor
    /// </summary>
    // [InitializeOnLoad]
    public static class TypescriptCompilationService {
        private const string TsCompilerService = "Typescript Compiler Service";

        public static bool IsWatchModeRunning => TypescriptCompilationServicesState.instance.CompilerCount > 0;
        public static int WatchCount => TypescriptCompilationServicesState.instance.CompilerCount;
        
        public static int ErrorCount {
            get {
                int count = 0;

                foreach (var watchState in TypescriptCompilationServicesState.instance.watchStates) {
                    if (watchState.IsActive && watchState.HasErrors) {
                        count += watchState.ErrorCount;
                    }
                }
                
                return count;
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
            var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            
            StopCompilers();
            
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories(includeFlags: TypescriptDirectorySearchFlags.NodeModules);
            foreach (var directory in typeScriptDirectories) {
                var watcher = new TypescriptCompilerWatchState(directory);
                if (watcher.Watch()) {
                    typeScriptServicesState.watchStates.Add(watcher);
                }
                else {
                    Debug.LogWarning($"Could not start compiler for {directory}");
                }
            }
            
            typeScriptServicesState.Update();
        }

        [MenuItem("Airship/TypeScript/Stop Compiler Services")]
        internal static void StopCompilers() {
            StopCompilerServices();
        }

        internal static void CompilationCompleted(TypescriptCompilerWatchState compiler) {
            var watchStates = TypescriptCompilationServicesState.instance.watchStates;
            if (watchStates.TrueForAll(state => !state.IsCompiling)) {
                // Debug.Log("Should refresh database!");
                // AssetDatabase.AllowAutoRefresh();
                // AssetDatabase.Refresh();
            }
        }

        internal static void StartCompilers(params TypescriptProject[] projects) {
            var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            foreach (var project in projects) {
                var watcher = new TypescriptCompilerWatchState(project.Directory);
                if (watcher.Watch()) {
                    typeScriptServicesState.watchStates.Add(watcher);
                }
                else {
                    Debug.LogWarning($"Could not start compiler for {project.Directory}");
                }
            }
        }

        internal static void StopCompilers(params TypescriptProject[] projects) {
            var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            foreach (var project in projects) {
                var watchState = typeScriptServicesState.GetWatchStateForProject(project);
                if (watchState != null && watchState.IsActive) {
                    watchState.CompilerProcess.Kill();
                    typeScriptServicesState.watchStates.Remove(watchState);
                }
            }
        }
        
        internal static void StopCompilerServices(bool shouldRestart = false) {
            var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            
            foreach (var compilerState in typeScriptServicesState.watchStates) {
                if (compilerState.processId == 0) continue;

                try {
                    var process = compilerState.CompilerProcess ?? Process.GetProcessById(compilerState.processId);
                    process.Kill();
                }
                catch {}
            }

            if (shouldRestart) { 
                Debug.LogWarning("Detected script reload - watch state for compiler(s) were restarted");
                
                foreach (var compilerState in typeScriptServicesState.watchStates) {
                    compilerState.Watch();
                }
            }
            else {
                typeScriptServicesState.watchStates.Clear();
            }

            // typeScriptServicesState.Update();
        }

        private static bool _compiling;
        private static readonly GUIContent BuildButtonContent; 
        private static readonly GUIContent CompileInProgressContent;

        internal static void FindTypescriptDir() {
            
        }
        
        internal static void CompileTypeScriptProject(string packageDir, TypeScriptCompileFlags compileFlags) {
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

                UpdateCompilerProgressBarText($"Install packages for {packageInfo.Name}...");
                var success = RunNpmInstall(packageDir);
                if (!success)
                {
                    Debug.LogWarning("Failed to install NPM dependencies");
                    _compiling = false;
                    return;
                }

                UpdateCompilerProgressBarText($"Compiling {packageInfo.Name}...");
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

        private static bool showProgressBar = false;
        private static float progress = 0;
        private static void UpdateCompilerProgressBar(float progress, string text) {
            showProgressBar = true;
            TypescriptCompilationService.progress = progress;
            EditorUtility.DisplayProgressBar(TsCompilerService, text, progress);
        }
        
        private static void UpdateCompilerProgressBarText(string text) {
            if (!showProgressBar) return;
            EditorUtility.DisplayProgressBar(TsCompilerService, text, TypescriptCompilationService.progress);
        }
        
        internal static void CompileTypeScript(TypeScriptCompileFlags compileFlags = 0) {
            var displayProgressBar = (compileFlags & TypeScriptCompileFlags.DisplayProgressBar) != 0;

            var gameConfig = GameConfig.Load();
            if (!gameConfig) {
                Debug.LogError("Failed to load gameConfig for compilation step");
                return;
            }
            
            var packages = gameConfig.packages;
            
            var isRunningServices = TypescriptCompilationServicesState.instance.CompilerCount > 0;
            if (isRunningServices) StopCompilerServices();
            
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
            showProgressBar = false;
            
            if (isRunningServices && EditorIntegrationsConfig.instance.typescriptAutostartCompiler) {
                StartCompilerServices();
            }
        }

        private static bool RunNpmInstall(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "install");
        }

        private static bool RunNpmBuild(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "run build");
        }
        
        internal static Process RunNodeCommand(string dir, string command, bool displayOutput = true) { 
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && node {command}\"";

            var procStartInfo = new ProcessStartInfo( "/bin/zsh", $"{command}")
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
            var procStartInfo = new ProcessStartInfo("node.exe", $"{command}")
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
            
            return proc;
        }

        private static Regex compilationStartRegex = new Regex(@"\[.*\] File change detected\.");
        private static Regex compilationFinishRegex = new Regex(@"\[.*\] Found (\d+) errors*.");

        struct CompilerJsonData {
            public string[] reimportFiles;
        }

        internal static void AttachWatchOutputToUnityConsole(TypescriptCompilerWatchState state, Process proc) {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var package = NodePackages.ReadPackageJson(state.directory);
            var id = package != null ? package.Name : state.directory;
            
            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                if (data.Data == "") return;

                if (data.Data.StartsWith("@json")) {
                    var jsonData = JsonConvert.DeserializeObject<CompilerJsonData>(data.Data.Substring(6));
                    if (jsonData.reimportFiles != null) {
                        foreach (var file in jsonData.reimportFiles)
                        {
                            Debug.Log("Force update " + file);
                            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
                        }
                    }
                    
                    return;
                }

                var prefix = $"<color=#8e8e8e>{id.PadLeft(TypescriptProjectsService.MaxPackageNameLength).Substring(0, TypescriptProjectsService.MaxPackageNameLength)}</color>";
                
                if (compilationStartRegex.IsMatch(data.Data)) {
                    state.compilationState = CompilationState.IsCompiling;
                }

                var test = compilationFinishRegex.Match(data.Data);
                if (test.Success) {
                    var compilationErrors = int.Parse(test.Groups[1].Value);
                    if (compilationErrors > 0) {
                        state.compilationState = CompilationState.HasErrors;
                        Debug.Log($"{prefix} <color=#ff534a>{compilationErrors} Compilation Error{(compilationErrors != 1 ? "s" : "")}</color>");
                    }
                    else {
                        state.compilationState = CompilationState.IsStandby;
                        CompilationCompleted(state);
                        Debug.Log($"{prefix} <color=#77f777>Compiled Successfully</color>");
                    }
                    
                    state.ErrorCount = compilationErrors;
                }
                else {
                    Debug.Log($"{prefix} {TerminalFormatting.Linkify(state.directory, TerminalFormatting.TerminalToUnity(data.Data))}");
                }
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
        SkipPackages = 1 << 3,
    }
}