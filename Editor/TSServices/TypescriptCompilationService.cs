using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Code.Bootstrap;
using CsToTs.TypeScript;
using Editor;
using Editor.Packages;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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

        public Process CompilerProcess { get; private set; }
        public bool IsActive => CompilerProcess is { HasExited: false };
        public bool IsCompiling => compilationState == CompilationState.IsCompiling;
        public bool HasErrors => compilationState == CompilationState.HasErrors;
        
        internal HashSet<TypescriptProblemItem> problemItems = new();
        
        public int ErrorCount { get; internal set; }
        
        public TypescriptCompilerWatchState(TypescriptProject project) {
            this.directory = project.Directory;
        }

        public bool Watch(TypescriptCompilerBuildArguments arguments) {
            return ThreadPool.QueueUserWorkItem(delegate {
                compilationState = CompilationState.IsCompiling;
                CompilerProcess = TypescriptCompilationService.RunNodeCommand(this.directory, $"{EditorIntegrationsConfig.TypeScriptLocation} {arguments.ToArgumentString(CompilerBuildMode.Watch)}");
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

        // public TypescriptCompilerWatchState GetWatchStateForProject(TypescriptProject project) {
        //     return watchStates.Find(f => f.project == project);
        // }
        
        internal void Update() {
            Save(true);
        }
    }

    public enum TypescriptProblemType {
        Error,
    }

    public struct TypescriptLocation : IEquatable<TypescriptLocation> {
        public int Line;
        public int Column;

        public bool Equals(TypescriptLocation other) {
            return this.Line == other.Line && this.Column == other.Column;
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

        public static bool IsCurrentlyCompiling =>
            TypescriptCompilationServicesState.instance.watchStates.Any(value => value.IsCompiling);
        
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

            var project = TypescriptProjectsService.Project;
            
            var watchState = new TypescriptCompilerWatchState(project); // We only need to watch at the main directory here.

            var watchArgs = new TypescriptCompilerBuildArguments() {
                Project = project.Directory,
                Package = "Typescript~", // We're using the 'Typescript~' directory for the packages
                Json = true, // We want the JSON event system here :-)
            };
            
            Debug.Log($"Watch args: {watchArgs.ToArgumentString(CompilerBuildMode.Watch)}");
            
            if (watchState.Watch(watchArgs)) {
                typeScriptServicesState.watchStates.Add(watchState);
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

        internal static void StopCompilers(params TypescriptProject[] projects) {
            // var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            // foreach (var project in projects) {
            //     var watchState = typeScriptServicesState.GetWatchStateForProject(project);
            //     if (watchState != null && watchState.IsActive) {
            //         watchState.CompilerProcess.Kill();
            //         typeScriptServicesState.watchStates.Remove(watchState);
            //     }
            // }
        }
        
        internal static void StopCompilerServices(bool shouldRestart = false) {
            var typeScriptServicesState = TypescriptCompilationServicesState.instance;
            
            TypescriptProjectsService.ReloadProjects();
            
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
                
                // TODO: Fix
                // foreach (var compilerState in typeScriptServicesState.watchStates) {
                //     compilerState.Watch();
                // }
            }
            else {
                typeScriptServicesState.watchStates.Clear();
            }
        }

        private static bool _compiling;
        private static readonly GUIContent BuildButtonContent; 
        private static readonly GUIContent CompileInProgressContent;

        internal static void FindTypescriptDir() {
            
        }
        
        internal static void CompileTypeScriptProject(string packageDir, TypeScriptCompileFlags compileFlags) {
            var shouldClean = (compileFlags & TypeScriptCompileFlags.FullClean) != 0;
            var skipInstalled = !shouldClean && (compileFlags & TypeScriptCompileFlags.Setup) != 0;

            if (!showProgressBar && (compileFlags & TypeScriptCompileFlags.DisplayProgressBar) != 0) {
                UpdateCompilerProgressBar(0f, $"Starting compilation for project...");
            }
            
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
                
                var isVerbose = EditorIntegrationsConfig.instance.typescriptVerbose;
                
                var compilerProcess = TypescriptCompilationService.RunNodeCommand(packageDir, $"{EditorIntegrationsConfig.TypeScriptLocation} {string.Join(' ', EditorIntegrationsConfig.instance.TypeScriptBuildArgs)}");
                AttachBuildOutputToUnityConsole(compilerProcess, packageDir);
                compilerProcess.WaitForExit();


                
                UpdateCompilerProgressBarText($"Checking types for {packageInfo.Name}...");
                if (packageInfo.Scripts.ContainsKey("types") && packageInfo.DevDependencies.ContainsKey("ts-patch")) {
                    UpdateCompilerProgressBarText($"Preparing types for {packageInfo.Name}...");              
                    var prepareTypes = TypescriptCompilationService.RunNodeCommand(packageDir, $"{EditorIntegrationsConfig.TypeScriptLocation} prepareTypes");
                    AttachBuildOutputToUnityConsole(prepareTypes, packageDir);
                    prepareTypes.WaitForExit();
                    
                    UpdateCompilerProgressBarText($"Generating types for {packageInfo.Name}...");
                    var generateTypes = TypescriptCompilationService.RunNodeCommand(packageDir, $"./node_modules/ts-patch/bin/tspc.js --build tsconfig.types.json {(isVerbose ? "--verbose" : "")}");
                    AttachBuildOutputToUnityConsole(generateTypes, packageDir);
                    generateTypes.WaitForExit();
                    
                    UpdateCompilerProgressBarText($"Running post types for {packageInfo.Name}...");
                    var postTypes = TypescriptCompilationService.RunNodeCommand(packageDir, $"{EditorIntegrationsConfig.TypeScriptLocation} postTypes");
                    AttachBuildOutputToUnityConsole(postTypes, packageDir);
                    postTypes.WaitForExit();
                }
                
                _compiling = false;
                if (compilerProcess.ExitCode == 0)
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

        private static bool RunNpmBuild(string dir) {
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

        internal struct CompilerEmitResult {
            public (CompilationState compilationState, int errorCount)? CompilationState;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        private enum CompilerEventType {
            Unknown,
            WatchReport,
            FileDiagnostic,
            CompiledFile,
            TransformFile,
            StartingCompile,
            FinishedCompile,
            FinishedCompileWithErrors,
        }
        
        private class CompilerEvent {
            [JsonProperty("event")]
            public CompilerEventType Event { get; set; }

            [JsonProperty("arguments")]
            public JObject Arguments { get; set; }
        }
        
        [CanBeNull] 
        private static CompilerEmitResult? HandleTypescriptOutput(TypescriptProject project, string message) {
            if (message == null || message == "") return null;
            var result = new CompilerEmitResult();

            // var id = package.Name;
            var prefix = $"<color=#8e8e8e>{project.Package.Name}</color>";
            //

            if (message.StartsWith("{")) {
                if (EditorIntegrationsConfig.instance.typescriptVerbose) Debug.Log($"JSON string: '{message}'");
                
                var jsonData = JsonConvert.DeserializeObject<CompilerEvent>(message);
                if (jsonData.Event == CompilerEventType.StartingCompile) {
                    var arguments = jsonData.Arguments.ToObject<CompilerStartCompilationEvent>();
                    if (arguments.Initial) {
                        Debug.Log($"{prefix} Starting compilation...");
                    }
                    else {
                        Debug.Log($"{prefix} File change(s) detected, recompiling files...");
                    }
                    
                    
                    project.ClearAllProblems();
                    TypescriptServicesStatusWindow.Reload();
                } else if (jsonData.Event == CompilerEventType.FileDiagnostic) {
                    var arguments = jsonData.Arguments.ToObject<CompilerEditorFileDiagnosticEvent>();
                    
                    var problemItem = TypescriptProblemItem.FromDiagnosticEvent(arguments);
                    project.AddProblemItem("", problemItem);

                    Debug.LogError(@$"{prefix} {ConsoleFormatting.TypescriptMessage(problemItem)}");
                } else if (jsonData.Event == CompilerEventType.FinishedCompileWithErrors) {
                    var arguments = jsonData.Arguments.ToObject<CompilerFinishCompilationWithErrorsEvent>();
                    Debug.Log($"{prefix} <color=#ff534a>{arguments.ErrorCount} Compilation Error{(arguments.ErrorCount != 1 ? "s" : "")}</color>");
                } else if (jsonData.Event == CompilerEventType.FinishedCompile) {
                    Debug.Log($"{prefix} <color=#77f777>Compiled Successfully</color>");
                }
            }
            else {
                // Handle string emit
                 if (compilationStartRegex.IsMatch(message)) { 
                     result.CompilationState = (CompilationState.IsCompiling, 0);
                     project.ClearAllProblems();
                }

                var test = compilationFinishRegex.Match(message);
                if (test.Success) {
                    var compilationErrors = int.Parse(test.Groups[1].Value);
                    if (compilationErrors > 0) {
                        result.CompilationState = (CompilationState.HasErrors, compilationErrors);
                        
                        Debug.Log($"{prefix} <color=#ff534a>{compilationErrors} Compilation Error{(compilationErrors != 1 ? "s" : "")}</color>");
                    }
                    else {
                       // project.ClearAllProblems();
                        result.CompilationState = (CompilationState.IsStandby, 0);
                        Debug.Log($"{prefix} <color=#77f777>Compiled Successfully</color>");
                    }
                }
                else {
                    var fileLink = TerminalFormatting.FileLink.Parse(message);

                    if (fileLink.HasValue) {
                        var errorItem = TypescriptProblemItem.Parse(message);
                        if (errorItem != null) {
                            //project.AddProblemItem(fileLink.Value.FilePath, errorItem);
                            Debug.LogError($"{prefix} {TerminalFormatting.Linkify(project.Directory, TerminalFormatting.TerminalToUnity(message), fileLink)}");
                            return result;
                        }
                    }
                    
                    Debug.Log($"{prefix} {TerminalFormatting.Linkify(project.Directory, TerminalFormatting.TerminalToUnity(message), fileLink)}");
                }
            }

            return result;
        }
        
        internal static void AttachBuildOutputToUnityConsole(Process proc, string directory) {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            
            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                if (data.Data == "") return;
                // HandleTypescriptOutput(data.Data);
            };
            
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };
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

                var emitResult = HandleTypescriptOutput(TypescriptProjectsService.Project, data.Data);
                if (emitResult?.CompilationState != null) {
                    var (compilationState, errorCount) = emitResult.Value.CompilationState.Value;
                    if (compilationState == CompilationState.IsStandby) {
                        CompilationCompleted(state);
                    }

                    state.ErrorCount = errorCount;
                    state.compilationState = compilationState;
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