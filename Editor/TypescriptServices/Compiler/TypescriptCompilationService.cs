using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Code.Authentication;
using Code.Bootstrap;
using Code.Util;
using CsToTs.TypeScript;
using Editor;
using Editor.Packages;
using Editor.Util;
using JetBrains.Annotations;
using Luau;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

    namespace Airship.Editor {
        public enum TypescriptCompilerState {
            /// <summary>
            /// The compiler is not running
            /// </summary>
            Inactive,
            /// <summary>
            /// The compiler is active, but idle
            /// </summary>
            Idle,
            /// <summary>
            /// The compiler is compiling files
            /// </summary>
            Compiling,
            /// <summary>
            /// The compiler has just compiled files, and is performing post-compile steps such as importing and reconcilation
            /// </summary>
            PostCompile,
            /// <summary>
            /// The compiler has crashed
            /// </summary>
            Crashed,
            Starting
        }
        
        /// <summary>
        /// Services relating to the TypeScript compiler in the editor
        /// </summary>
        // [InitializeOnLoad]
        public static class TypescriptCompilationService {
            private const int ExitCodeKill = 137;
            private const string TsCompilerService = "Compiling Scripts";
            
            /// <summary>
            /// True if the compiler is running in watch mode
            /// </summary>
            public static bool IsWatchModeRunning => TypescriptCompilationServicesState.instance.CompilerCount > 0;
            public static bool Crashed { get; private set; }

            /// <summary>
            /// The last DateTime the compiler compiled files
            /// </summary>
            public static DateTime LastCompiled { get; private set; }

            /// <summary>
            /// A boolean representing if the compiler is currently compiling files
            /// </summary>
            internal static bool IsCompilingFiles { get; private set; }
            internal static bool IsImportingFiles { get; private set; }
            internal static bool IsStartingUp { get; private set; }
            
            public static TypescriptCompilerState CompilerState {
                get {
                    if (IsStartingUp) return TypescriptCompilerState.Starting;
                    if (Crashed) return TypescriptCompilerState.Crashed;
                    if (IsCompilingFiles) return TypescriptCompilerState.Compiling;
                    if (IsImportingFiles) return TypescriptCompilerState.PostCompile;
                    if (IsWatchModeRunning) return TypescriptCompilerState.Idle;

                    return TypescriptCompilerState.Inactive;
                }
            }

            /// <summary>
            /// The path to where utsc-dev should be installed (if applicable)
            /// </summary>
            internal static string DevelopmentCompilerPath {
                get {
#if UNITY_EDITOR_OSX
                    var compilerPath = "/usr/local/lib/node_modules/roblox-ts-dev/utsc-dev.js";
#else
                    var compilerPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                       "/npm/node_modules/roblox-ts-dev/utsc-dev.js";
#endif
                    return compilerPath;
                }
            }

            /// <summary>
            /// The path to the internal build of utsc
            /// </summary>
            internal static string EditorCompilerPath =>
                Path.GetFullPath("Packages/gg.easy.airship/Editor/TypescriptCompiler~/utsc.js");
            
            /// <summary>
            /// True if the user has the developer compiler installed on their system
            /// </summary>
            public static bool HasDevelopmentCompiler => DevelopmentCompilerPath != null && File.Exists(DevelopmentCompilerPath);
            
            private const string AirshipCompilerVersionKey = "airshipCompilerVersion0";
            private const string AirshipPreventCompileOnPlayKey = "airshipPreventCompileOnPlay";
            
            private const TypescriptCompilerVersion DefaultVersion = TypescriptCompilerVersion.UseEditorVersion;
            
#pragma warning disable CS0612 // Type or member is obsolete
            
            /// <summary>
            /// The version of the compiler the user is using
            /// </summary>
            internal static TypescriptCompilerVersion CompilerVersion {
                get {
                    if (!EditorPrefs.HasKey(AirshipCompilerVersionKey)) {
                        EditorPrefs.SetInt(AirshipCompilerVersionKey, (int) DefaultVersion);
                        return DefaultVersion;
                    }
                    else {
                        return (TypescriptCompilerVersion)EditorPrefs.GetInt(AirshipCompilerVersionKey,
                            (int)DefaultVersion);
                    }
                }
                set => EditorPrefs.SetInt(AirshipCompilerVersionKey, (int) value);
            }

            internal static bool ShowDeveloperOptions {
                set => EditorPrefs.SetBool("airshipTypescriptDeveloperOptions", value);
                get {
                    if (EditorPrefs.HasKey("airshipTypescriptDeveloperOptions")) {
                        return EditorPrefs.GetBool("airshipTypescriptDeveloperOptions");
                    }

                    return false;
                }
            }
            
            internal static bool PreventPlayModeWithErrors {
                get {
                    if (!EditorPrefs.HasKey(AirshipPreventCompileOnPlayKey)) {
                        return true;
                    }
                    else {
                        return EditorPrefs.GetBool(AirshipPreventCompileOnPlayKey);
                    }
                }
                set => EditorPrefs.SetBool(AirshipPreventCompileOnPlayKey, value);
            }

            internal static TypescriptCompilerVersion[] UsableVersions {
                get {
                    var versions = new List<TypescriptCompilerVersion> { TypescriptCompilerVersion.UseEditorVersion };

                    if (File.Exists(DevelopmentCompilerPath)) {
                        versions.Add(TypescriptCompilerVersion.UseLocalDevelopmentBuild);
                    }

                    return versions.ToArray();
                }
            }
            
            /// <summary>
            /// The location of the current compiler the user is using
            /// </summary>
            public static string TypeScriptLocation {
                get {
                    switch (CompilerVersion) {
                        case TypescriptCompilerVersion.UseLocalDevelopmentBuild:
                            return DevelopmentCompilerPath;
                        
                        case TypescriptCompilerVersion.UseEditorVersion:
                            return EditorCompilerPath;
                        default:
                            throw new ArgumentException($"Invalid Compiler Type Index {CompilerVersion}");
                    }
                }   
            }

            /// <summary>
            /// Gets the operating-system friendly location for command-line usage
            /// </summary>
            internal static string TypescriptLocationCommandLine {
                get {
                    var path = TypeScriptLocation;
                    // ReSharper disable once ConvertIfStatementToReturnStatement
                    if (!path.Contains(" ")) return path;
#if UNITY_EDITOR_WIN
                    return $"\"{path}\"";
#else
                    return path.Replace(" ", "\\ ");                
#endif
                }
            }
            
#pragma warning restore CS0612 // Type or member is obsolete

            private static double lastChecked = 0;
            private const double checkInterval = 5;
            private static bool queueActive = false;
            
            private static List<string> CompiledFileQueue = new();
            private static void ReimportCompiledFiles() {
                IsImportingFiles = true;
                AirshipReconciliationService.StartScriptUpdates();
                
                if (!(EditorApplication.timeSinceStartup > lastChecked + checkInterval)) return;
                lastChecked = EditorApplication.timeSinceStartup;
                    
                // No point importing files if there's errors, or it's empty
                if (ErrorCount > 0 || CompiledFileQueue.Count == 0) {
                    EditorApplication.update -= ReimportCompiledFiles;
                    return;
                }

                var artifacts = AirshipLocalArtifactDatabase.instance;
                var modifiedDatabase = false;
                
                try {
                    AssetDatabase.StartAssetEditing();
                    var compileFileList = CompiledFileQueue.ToArray();
                    
                    foreach (var file in compileFileList) {
                        // var outFileHash = TypescriptProjectsService.Project.GetOutputFileHash(file);
                        //
                        // if (artifacts.TryGetScriptAssetDataFromPath(PosixPath.ToPosix(file), out var data)) {
                        //     if (string.IsNullOrEmpty(outFileHash) || string.IsNullOrEmpty(data.metadata.compiledHash) || outFileHash != data.metadata.compiledHash) {
                        //         AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
                        //         data.metadata.compiledHash = outFileHash;
                        //         modifiedDatabase = true;
                        //     }
                        // }
                        // else {
                        //     var scriptData = artifacts.GetOrCreateScriptAssetData(AssetDatabase.LoadAssetAtPath<AirshipScript>(file));
                        //     scriptData.metadata = new TypescriptCompilerMetadata() {
                        //         compiledHash = outFileHash
                        //     };
                            
                            AssetDatabase.ImportAsset(file, ImportAssetOptions.Default);
                        //     modifiedDatabase = true;
                        // }
                    }
                    
                    AssetDatabase.Refresh();
                } catch (Exception ex) {
                    Debug.LogException(ex);
                } finally {
                    AssetDatabase.StopAssetEditing();
                    if (modifiedDatabase) {
                        artifacts.Modify();
                    }
                }
                
                EditorApplication.update -= ReimportCompiledFiles;
                queueActive = false;
                
                IsImportingFiles = false;
                AirshipReconciliationService.StopScriptUpdates();
                CompiledFileQueue.Clear();
            }

            public static void QueueCompiledFileForImport(string file) {
                var relativePath = Path.Join("Assets", Path.GetRelativePath(Application.dataPath, file));
                
                if (!CompiledFileQueue.Contains(relativePath)) {
                    CompiledFileQueue.Add(relativePath);
                }
            }
            
            public static void QueueReimportFiles() {
                if (queueActive) return;
                queueActive = true;
                EditorApplication.update += ReimportCompiledFiles;
            }
            
            public static int ErrorCount => TypescriptProjectsService.Projects.Sum(project => project.ErrorCount);

            internal static void FullRebuild() {
                var flags = TypeScriptCompileFlags.FullClean;
                if (EditorIntegrationsConfig.instance.typescriptIncremental) {
                    flags |= TypeScriptCompileFlags.Incremental;
                }
                
                BuildTypescript(flags);
            }
            
            internal static TypescriptCompilerBuildArguments WatchArgs { get; private set; }
            internal static NodeJsArguments NodeJsArguments { get; private set; }
            
            internal static void StartCompilerServices() {
                if (IsStartingUp) return;
                IsStartingUp = true;
                TypescriptLogService.StartLogging();
                StopCompilers();
                
                var project = TypescriptProjectsService.Project;
                if (project == null) return;
                
                var watchState = new TypescriptCompilerWatchState(project); // We only need to watch at the main directory here.

                var watchArgs = new TypescriptCompilerBuildArguments() {
                    Project = project.Directory,
                    Json = true, // We want the JSON event system here :-)
                    // WriteOnlyChanged = true,
                    Verbose = EditorIntegrationsConfig.instance.typescriptVerbose,
                    Incremental = EditorIntegrationsConfig.instance.typescriptIncremental,
                };

                var nodeJsArgs = new NodeJsArguments();
                if (TypescriptServicesLocalConfig.instance.overrideMemory) {
                    nodeJsArgs.MaxOldSpaceSize = TypescriptServicesLocalConfig.instance.overrideMemoryMb;
                }

                if (TypescriptServicesLocalConfig.instance.useNodeInspect && TypescriptCompilationService.CompilerVersion == TypescriptCompilerVersion.UseLocalDevelopmentBuild) {
                    nodeJsArgs.Inspect = true;
                }

                WatchArgs = watchArgs;
                NodeJsArguments = nodeJsArgs;
                
                EditorCoroutines.Execute(watchState.Watch(watchArgs, nodeJsArgs));
                TypescriptLogService.Log(TypescriptLogLevel.Information, "Started compiler services.");
                
                TypescriptServices.IsCompilerStoppedByUser = false;
                IsStartingUp = false;
            }
            
            internal static void StopCompilers() {
                StopCompilerServices();
            }

            internal static void ClearIncrementalCache() {
                var buildInfo = TypescriptProjectsService.Project?.BuildInfoPath;
                if (buildInfo == null) return;
                if (!File.Exists(buildInfo)) return;

                File.Delete(buildInfo);
            }

            internal static bool RestartCompilers(Action action = null) {
                var wasRunning = IsWatchModeRunning;
                if (wasRunning) {
                    StopCompilers();
                }
                
                action?.Invoke();
                
                if (wasRunning) {
                    StartCompilerServices();
                }

                return wasRunning;
            }

            internal static void StopCompilerServices(bool shouldRestart = false) {
                if (!IsWatchModeRunning) return;
                IsStartingUp = false;
                var typeScriptServicesState = TypescriptCompilationServicesState.instance;
                
                foreach (var compilerState in typeScriptServicesState.watchStates.ToList()) {
                    compilerState.Stop(); // test
                }

                var project = TypescriptProjectsService.Project;
                if (project != null && Progress.Exists(project.ProgressId)) {
                    Progress.Finish(project.ProgressId, Progress.Status.Canceled);
                }
                
                TypescriptLogService.Log(TypescriptLogLevel.Warning, "Stopped compiler services.");
                
                if (shouldRestart) {
                    StartCompilerServices();
                }
            }
            
            private static readonly GUIContent BuildButtonContent; 
            private static readonly GUIContent CompileInProgressContent;
            
            private static void CompileTypeScriptProject(TypescriptProject project, TypescriptCompilerBuildArguments arguments, TypeScriptCompileFlags compileFlags) {
                var compilationState = project.CompilationState;
                compilationState.CompileFlags = compileFlags;
                
                var fullClean = (compileFlags & TypeScriptCompileFlags.FullClean) != 0;
                var setup = (compileFlags & TypeScriptCompileFlags.Setup) != 0;
                var skipInstalled = !fullClean && setup;
               

                if (!showProgressBar && (compileFlags & TypeScriptCompileFlags.DisplayProgressBar) != 0) {
                    showProgressBar = true;
                    UpdateCompilerProgressBar(0f, $"Starting to compile TypeScript code...");
                }
                
                if (!File.Exists(Path.Join(project.Package.Directory, "package.json"))) {
                    return;
                }

                var packageInfo = project.Package;
                var packageDir = packageInfo.Directory;

                var outPath = Path.Join(packageDir, "out");
                if (fullClean && Directory.Exists(outPath))
                {
                    Directory.Delete(outPath, true);
                }

                var installed = Directory.Exists(Path.Join(packageDir, "node_modules"));
                if (skipInstalled && installed) {
                    return; // ??
                }

                try
                {
                    if (fullClean) {
                        UpdateCompilerProgressBarText($"Preparing TypeScript project");
                        var success = RunNpmInstall(packageDir);
                        if (!success)
                        {
                            Debug.LogWarning("Failed to install NPM dependencies");
                            return;
                        }
                    }

                    compilationState.FilesToCompileCount = 0;
                    compilationState.CompiledFileCount = 0;
       
                    var compilerProcess = RunNodeCommand(project.Directory, $"{TypescriptLocationCommandLine} {arguments.GetCommandString(CompilerCommand.BuildOnly)}");
                    AttachBuildOutputToUnityConsole(project, arguments, compilerProcess, packageDir);
                    
                    while (!compilerProcess.HasExited) {
                        if (compilationState.FilesToCompileCount == 0) continue;
                        UpdateCompilerProgressBar(
                            compilationState.CompiledFileCount / (float)compilationState.FilesToCompileCount, $"Compiling TypeScript files {compilationState.CompiledFileCount}/{project.CompilationState.FilesToCompileCount}...");
                    }
                    
                    // compilerProcess.WaitForExit();
                    
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
                TypescriptCompilationService.progress = progress;
                if (!showProgressBar) return;
                EditorUtility.DisplayProgressBar(TsCompilerService, text, progress);
            }
            
            private static bool UpdateCompilerProgressBarCancellable(float progress, string text) {
                TypescriptCompilationService.progress = progress;
                return showProgressBar && EditorUtility.DisplayCancelableProgressBar(TsCompilerService, text, progress);
            }
            
            private static void UpdateCompilerProgressBarText(string text, float? value = null) {
                if (!showProgressBar) return;
                EditorUtility.DisplayProgressBar(TsCompilerService, text, value ?? progress);
            }

            internal static void BuildTypescript(TypeScriptCompileFlags flags = 0) {
                BuildTypescript(new []{ TypescriptProjectsService.Project }, flags);
            }
            internal static void BuildTypescript(TypescriptProject[] projects, TypeScriptCompileFlags compileFlags = 0) {
                var isRunningServices = TypescriptCompilationServicesState.instance.CompilerCount > 0;
                if (isRunningServices) StopCompilerServices();


                
                var compiled = 0;
                var totalCompileCount = projects.Length;
                foreach (var project in projects) {
                    if ((compileFlags & TypeScriptCompileFlags.FullClean) != 0) {
                        if (File.Exists(project.BuildInfoPath)) {
                            File.Delete(project.BuildInfoPath);
                        }
                    }
                    
                    var buildArguments = new TypescriptCompilerBuildArguments() {
                        Project = project.Directory,
                        Package = project.TsConfig.airship.PackageFolderPath,
                        Json = true,
                        Publishing = (compileFlags & TypeScriptCompileFlags.Publishing) != 0,
                        Incremental = (compileFlags & TypeScriptCompileFlags.Incremental) != 0,
                        Verbose = (compileFlags & TypeScriptCompileFlags.Verbose) != 0 || EditorIntegrationsConfig.instance.typescriptVerbose,
                    };
                    
                    CompileTypeScriptProject(project, buildArguments, compileFlags); 
                }

                if (!showProgressBar) return;
                showProgressBar = false;
                EditorUtility.ClearProgressBar();
            }

            private static bool RunNpmInstall(string dir)
            {
                return NodePackages.RunNpmCommand(dir, "install");
            }

            private static bool RunNpmBuild(string dir) {
                return NodePackages.RunNpmCommand(dir, "run build");
            }

            internal static Process RunNodeCommand(string dir, string command, bool displayOutput = true) {
#if UNITY_EDITOR_WIN
                // Windows uses the .exe
                var procStartInfo = ShellProcess.GetStartInfoForCommand(dir, "node.exe", command);
#else
                var procStartInfo = ShellProcess.GetShellStartInfoForCommand(command, dir);
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
                CompiledFileWrite,
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
                var isUsingProgressBar = showProgressBar;
                
                if (string.IsNullOrEmpty(message)) return null;
                if (project == null) return null;
                
                var result = new CompilerEmitResult();
                var prefix = $"<color=#8e8e8e>TS</color>";

                if (message.StartsWith("{")) {
                    var jsonData = JsonConvert.DeserializeObject<CompilerEvent>(message);
                    if (jsonData.Event == CompilerEventType.StartingCompile) {
                        IsCompilingFiles = true;

                        var arguments = jsonData.Arguments.ToObject<CompilerStartCompilationEvent>();
                        project.CompilationState.FilesToCompileCount = arguments.Count;
                        project.CompilationState.CompiledFileCount = 0;
                        project.ProgressId = Progress.Start($"Compiling TypeScript",
                            $"Compiling {arguments.Count} TypeScript Files");

                        TypescriptLogService.StartCompileStopWatch();
                        TypescriptLogService.Log(TypescriptLogLevel.Information, 
                            $"Starting compilation of {arguments.Count} files in project...");
                        
                        if (arguments.Count != 0) {
                            if (arguments.Initial) {
                                Debug.Log($"{prefix} Starting compilation of {arguments.Count} files...");
                                project.CompilationState.RequiresInitialCompile = true;
                            }
                            else {
                                Debug.Log($"{prefix} File change(s) detected, recompiling files...");
                            }
                        }


                        project.ClearAllProblems();
                    }
                    else if (jsonData.Event == CompilerEventType.FileDiagnostic) {
                        var arguments = jsonData.Arguments.ToObject<CompilerEditorFileDiagnosticEvent>();

                        var problemItem = TypescriptFileDiagnosticItem.FromDiagnosticEvent(project, arguments);
                        project.AddProblemItem("", problemItem);
                        
                        TypescriptLogService.LogFileDiagnostic(problemItem);

                        switch (problemItem.ProblemType) {
                            case TypescriptProblemType.Fatal:
                            case TypescriptProblemType.Error:
                                Debug.LogError(@$"{prefix} {ConsoleFormatting.GetProblemItemString(problemItem)}");
                                break;
                            case TypescriptProblemType.Warning:
                                Debug.LogWarning(@$"{prefix} {ConsoleFormatting.GetProblemItemString(problemItem)}");
                                break;
                            case TypescriptProblemType.Suggestion:
                            case TypescriptProblemType.Message:
                                Debug.Log(@$"{prefix} {ConsoleFormatting.GetProblemItemString(problemItem)}");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                    }
                    else if (jsonData.Event == CompilerEventType.FinishedCompileWithErrors) {
                        Progress.Finish(project.ProgressId, Progress.Status.Failed);

                        var arguments = jsonData.Arguments.ToObject<CompilerFinishCompilationWithErrorsEvent>();
                        Debug.Log(
                            $"{prefix} <color=#ff534a>{arguments.ErrorCount} Compilation Error{(arguments.ErrorCount != 1 ? "s" : "")}</color>");

                        TypescriptLogService.Log(TypescriptLogLevel.Error, $"Finished compilation with {arguments.ErrorCount} errors. No files were changed.");
                        TypescriptLogService.StopCompileStopWatch();
                        
                        IsCompilingFiles = false;
                        LastCompiled = DateTime.Now;
                    }
                    else if (jsonData.Event == CompilerEventType.FinishedCompile) {
                        // AssetDatabase.StopAssetEditing();
                        Progress.Finish(project.ProgressId);

                        if (project.CompilationState.CompiledFileCount > 0) {
                            Debug.Log($"{prefix} <color=#77f777>Compiled Successfully</color>");
                            
                            TypescriptLogService.StopCompileStopWatch();
                            TypescriptLogService.Log(TypescriptLogLevel.Information, $"Finished compilation of {project.CompilationState.CompiledFileCount} files successfully.");

                            if ((project.CompilationState.CompileFlags & TypeScriptCompileFlags.SkipReimportQueue) ==
                                0) {
                                TypescriptLogService.Log(TypescriptLogLevel.Information, "Requesting reimport of files...");
                                QueueReimportFiles();
                            }
                        }

                        IsCompilingFiles = false;
                        LastCompiled = DateTime.Now;
                    }
                    else if (jsonData.Event == CompilerEventType.CompiledFile) {
                        var arguments = jsonData.Arguments.ToObject<CompiledFileEvent>();
                        var friendlyName = Path.GetRelativePath("Assets", arguments.fileName);

                        project.CompilationState.CompiledFileCount += 1;

                        var length = project.CompilationState.FilesToCompileCount.ToString().Length;
                        var compiledFileCountStr = project.CompilationState.CompiledFileCount.ToString();

                        Progress.Report(project.ProgressId, project.CompilationState.CompiledFileCount,
                            project.CompilationState.FilesToCompileCount);

                        TypescriptLogService.LogEvent(arguments);
                        
                        if (buildArguments.Verbose) {
                            Debug.Log(
                                @$"{prefix} [{compiledFileCountStr.PadLeft(length)}/{project.CompilationState.FilesToCompileCount}] Compiled {friendlyName}");
                        }

                        if (!WatchArgs.WriteOnlyChanged && ((project.CompilationState.CompileFlags & TypeScriptCompileFlags.SkipReimportQueue) == 0)) {
                            QueueCompiledFileForImport(arguments.fileName);
                        }
                    } else if (jsonData.Event == CompilerEventType.CompiledFileWrite) {
                        var arguments = jsonData.Arguments.ToObject<CompiledFileWriteEvent>();
                        
                        if (arguments.changed && (project.CompilationState.CompileFlags & TypeScriptCompileFlags.SkipReimportQueue) == 0)
                            QueueCompiledFileForImport(arguments.fileName);
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
                            var errorItem = TypescriptFileDiagnosticItem.Parse(message);
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
                proc.EnableRaisingEvents = true;
                
                proc.OutputDataReceived += (_, data) =>
                {
                    if (data.Data == null) return;
                    if (data.Data == "") return;

                    try {
                        HandleTypescriptOutput(project, buildArguments, data.Data);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
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
                proc.EnableRaisingEvents = true;
                Crashed = false;

                var project = TypescriptProjectsService.Project;
                project.CompilationState.CompileFlags = 0;
                project.CompilationState.FilesToCompileCount = 0;
                project.CompilationState.CompiledFileCount = 0;
                
                proc.OutputDataReceived += (_, data) =>
                {
                    try {
                        if (data.Data == null) return;
                        if (data.Data == "") return;

                        var emitResult = HandleTypescriptOutput(project, buildArguments, data.Data);
                        if (emitResult?.CompilationState != null) {
                            var (compilationState, errorCount) = emitResult.Value.CompilationState.Value;

                            state.ErrorCount = errorCount;
                            state.compilationState = compilationState;
                        }
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
                };

                var errorData = new List<string>();
                proc.ErrorDataReceived += (_, data) =>
                {
                    if (data.Data == null) return;
                    UnityEngine.Debug.LogWarning(data.Data);
                    errorData.Add(data.Data);
                };

                proc.Exited += (_, _) => {
                    if (proc.ExitCode <= 0) return;
                    if (proc.ExitCode == ExitCodeKill) return;
                    
                    Debug.Log("Compiler process exited with code " + proc.ExitCode);
                    var progressId = TypescriptProjectsService.Project!.ProgressId;
                    Crashed = true;
                    
                    project.CrashProblemItem =
                        new TypescriptCrashProblemItem(project,  errorData, $"The Typescript compiler unexpectedly crashed!\n(Exit Code {proc.ExitCode})", proc.ExitCode);
                    
                    if (Progress.Exists(progressId)) {
                        Progress.SetDescription(progressId, "Failed due to process exit - check console");
                        Progress.Finish(progressId, Progress.Status.Failed);
                    }
                    
                    state.processId = 0; // we've exited, no more process
                    TypescriptCompilationServicesState.instance.UnregisterWatchCompiler(state);
                };
            }
        }

        [Flags]
        public enum TypeScriptCompileFlags {
            FullClean = 1 << 0,
            Setup = 1 << 1,
            DisplayProgressBar = 1 << 2,
            SkipPackages = 1 << 3,
            Incremental = 1 << 4,
            Verbose = 1 << 5,
            Publishing = 1 << 6,
            SkipReimportQueue = 1 << 7,
        }
    }