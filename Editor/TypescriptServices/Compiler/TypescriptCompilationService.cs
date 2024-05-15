using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Code.Bootstrap;
using CsToTs.TypeScript;
using Editor;
using Editor.Packages;
using Editor.Util;
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

        public struct TypescriptLineAndColumn : IEquatable<TypescriptLineAndColumn> {
            public int Line;
            public int Column;

            public bool Equals(TypescriptLineAndColumn other) {
                return this.Line == other.Line && this.Column == other.Column;
            }
        }

        public struct TypescriptPosition : IEquatable<TypescriptPosition> {
            public int Position;
            public int Length;
            public string Text;

            public bool Equals(TypescriptPosition other) {
                return this.Position == other.Position;
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
                //CompileTypeScript(TypeScriptCompileFlags.Setup | TypeScriptCompileFlags.DisplayProgressBar);
            }

            [MenuItem("Airship/Full Script Rebuild")]
            internal static void FullRebuild() {
                CompileTypeScript(new[] { TypescriptProjectsService.Project }, TypeScriptCompileFlags.FullClean);
            }

            private static TypescriptCompilerWatchState watchProgram;
            
            [MenuItem("Airship/TypeScript/Start Compiler Services")]
            internal static void StartCompilerServices() {
                var typeScriptServicesState = TypescriptCompilationServicesState.instance;
                StopCompilers();

                var project = TypescriptProjectsService.Project;
                if (project == null) return;
                
                var watchState = new TypescriptCompilerWatchState(project); // We only need to watch at the main directory here.

                var watchArgs = new TypescriptCompilerBuildArguments() {
                    Project = project.Directory,
                    Json = true, // We want the JSON event system here :-)
                };

                if (watchState.Watch(watchArgs)) {
                    typeScriptServicesState.watchStates.Add(watchState);
                    watchProgram = watchState;
                }
                
                typeScriptServicesState.Update();
            }

            [MenuItem("Airship/TypeScript/Stop Compiler Services")]
            internal static void StopCompilers() {
                StopCompilerServices();
            }

            internal static void RequestCompileFile(string file) {
                Debug.Log("Recieved compile request for: " +  file);
                
                if (watchProgram != null) {
                    watchProgram.RequestCompileFiles(file);
                }
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
            
            internal static void CompileTypeScriptProject(TypescriptProject project, TypescriptCompilerBuildArguments arguments, TypeScriptCompileFlags compileFlags) {
                var shouldClean = (compileFlags & TypeScriptCompileFlags.FullClean) != 0;
                var skipInstalled = !shouldClean && (compileFlags & TypeScriptCompileFlags.Setup) != 0;

                if (!showProgressBar && (compileFlags & TypeScriptCompileFlags.DisplayProgressBar) != 0) {
                    UpdateCompilerProgressBar(0f, $"Starting compilation for project...");
                }
                
                if (!File.Exists(Path.Join(project.Package.Directory, "package.json"))) {
                    return;
                }

                var packageInfo = project.Package;
                var packageDir = project.Directory;

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

                    UpdateCompilerProgressBarText($"Compiling Typescript project...");
                    var compilerProcess = RunNodeCommand(packageDir, $"{EditorIntegrationsConfig.TypeScriptLocation} {arguments.ToArgumentString(CompilerBuildMode.BuildOnly)}");
                    AttachBuildOutputToUnityConsole(project, arguments, compilerProcess, packageDir);
                    compilerProcess.WaitForExit();
                    
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
            
            internal static void CompileTypeScript(TypescriptProject[] projects, TypeScriptCompileFlags compileFlags = 0) {
                var gameConfig = GameConfig.Load();
                if (!gameConfig) {
                    Debug.LogError("Failed to load gameConfig for compilation step");
                    return;
                }
                
                var isRunningServices = TypescriptCompilationServicesState.instance.CompilerCount > 0;
                if (isRunningServices) StopCompilerServices();
                
                UpdateCompilerProgressBar(0f, $"Compiling typeScript...");

                var compiled = 0;
                var totalCompileCount = projects.Length;
                foreach (var project in projects) {
                    var buildArguments = new TypescriptCompilerBuildArguments() {
                        Project = project.Directory,
                        Package = project.TsConfig.airship.PackageFolderPath,
                        Json = true,
                    };
                    
                    CompileTypeScriptProject(project, buildArguments, compileFlags); 
                    UpdateCompilerProgressBar((float) compiled / totalCompileCount, $"Compiled {project} ({compiled} of {totalCompileCount})");
                }
                
                EditorUtility.ClearProgressBar();
                showProgressBar = false;
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
            private static CompilerEmitResult? HandleTypescriptOutput(TypescriptProject project, TypescriptCompilerBuildArguments buildArguments, string message) {
                if (message == null || message == "") return null;
                var result = new CompilerEmitResult();
                Debug.Log($"Message {message}");

                // var id = package.Name;
                var prefix = $"<color=#8e8e8e>{project.Package.Name}</color>";
                //

                if (message.StartsWith("{")) {
                    var jsonData = JsonConvert.DeserializeObject<CompilerEvent>(message);
                    if (jsonData.Event == CompilerEventType.StartingCompile) {
                        var arguments = jsonData.Arguments.ToObject<CompilerStartCompilationEvent>();
                        if (arguments.Initial) {
                            Debug.Log($"{prefix} Starting compilation...");
                            project.RequiresInitialCompile = true;
                        }
                        else {
                            Debug.Log($"{prefix} File change(s) detected, recompiling files...");
                        }
                        
                        
                        project.ClearAllProblems();
                        // TypescriptServicesStatusWindow.Reload();
                    } else if (jsonData.Event == CompilerEventType.FileDiagnostic) {
                        var arguments = jsonData.Arguments.ToObject<CompilerEditorFileDiagnosticEvent>();

                        var problemItem = TypescriptProblemItem.FromDiagnosticEvent(arguments);
                        project.AddProblemItem("", problemItem);

                        Debug.LogError(@$"{prefix} {ConsoleFormatting.GetProblemItemString(problemItem)}");
                    } else if (jsonData.Event == CompilerEventType.FinishedCompileWithErrors) {
                        var arguments = jsonData.Arguments.ToObject<CompilerFinishCompilationWithErrorsEvent>();
                        Debug.Log($"{prefix} <color=#ff534a>{arguments.ErrorCount} Compilation Error{(arguments.ErrorCount != 1 ? "s" : "")}</color>");
                    } else if (jsonData.Event == CompilerEventType.FinishedCompile) {
                        Debug.Log($"{prefix} <color=#77f777>Compiled Successfully</color>");

                        if (!project.RequiresInitialCompile) {
                            AssetDatabase.StartAssetEditing();
                            TypescriptImporter.ReimportAllTypescript();
                            AssetDatabase.StopAssetEditing();
                        }
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
                            TypescriptImporter.ReimportAllTypescript();
                        }
                    }
                    else {
                        var fileLink = TerminalFormatting.FileLink.Parse(message);

                        if (fileLink.HasValue) {
                            var errorItem = TypescriptProblemItem.Parse(message);
                            if (errorItem != null) {
                                Debug.LogError($"{prefix} {TerminalFormatting.Linkify(project.Directory, TerminalFormatting.TerminalToUnity(message), fileLink)}");
                                return result;
                            }
                        }
                        
                        Debug.Log($"{prefix} {TerminalFormatting.Linkify(project.Directory, TerminalFormatting.TerminalToUnity(message), fileLink)}");
                    }
                }

                return result;
            }
            
            internal static void AttachBuildOutputToUnityConsole(
                TypescriptProject project, 
                TypescriptCompilerBuildArguments buildArguments, 
                Process proc, 
                string directory
            ) {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                
                proc.OutputDataReceived += (_, data) =>
                {
                    if (data.Data == null) return;
                    if (data.Data == "") return;
                    HandleTypescriptOutput(project, buildArguments, data.Data);
                };
                
                proc.ErrorDataReceived += (_, data) =>
                {
                    if (data.Data == null) return;
                    UnityEngine.Debug.LogWarning(data.Data);
                };
            }

            internal static void AttachWatchOutputToUnityConsole(TypescriptCompilerWatchState state, TypescriptCompilerBuildArguments buildArguments, Process proc) {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                
                proc.OutputDataReceived += (_, data) =>
                {
                    try {
                        if (data.Data == null) return;
                        if (data.Data == "") return;

                        var emitResult = HandleTypescriptOutput(TypescriptProjectsService.Project, buildArguments, data.Data);
                        if (emitResult?.CompilationState != null) {
                            var (compilationState, errorCount) = emitResult.Value.CompilationState.Value;
                            if (compilationState == CompilationState.IsStandby) {
                                CompilationCompleted(state);
                            }

                            state.ErrorCount = errorCount;
                            state.compilationState = compilationState;
                        }
                    }
                    catch (Exception e) {
                        Debug.LogError($"Got {e.GetType().Name}: {e.Message}");
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