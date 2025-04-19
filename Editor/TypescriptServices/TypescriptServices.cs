#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Editor;
using Editor.EditorInternal;
using Editor.Packages;
using HandlebarsDotNet;
using ParrelSync;
using Unity.EditorCoroutines.Editor;
using Unity.Multiplayer.Playmode;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Airship.Editor {
    [Flags]
    internal enum TypescriptExperiments {
        ReconcileOnPostCompile = 1 << 0,
    }

    [FilePath("Library/TypescriptServices", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptServicesLocalConfig : ScriptableSingleton<TypescriptServicesLocalConfig> {
        [SerializeField]
        internal bool hasInitialized = false;
        [SerializeField] internal bool usePostCompileReconciliation = true;

        private void OnEnable() {
            AirshipComponent.UsePostCompileReconciliation = usePostCompileReconciliation;
        }

        public void Modify() {
            AirshipComponent.UsePostCompileReconciliation = usePostCompileReconciliation;
            Save(true);
        }
    }
    
    internal delegate void CompilerCrashEvent(TypescriptCrashProblemItem problemItem);
    
    /// <summary>
    /// Main static class for handling the TypeScript services
    /// </summary>
    public static class TypescriptServices {
        internal static event CompilerCrashEvent CompilerCrash;
        
        
        /// <summary>
        /// Returns true if this is a valid editor window to run TSS in
        /// </summary>
        public static bool IsValidEditor =>
            !ClonesManager.IsClone() &&
            !Environment.GetCommandLineArgs().Contains("--virtual-project-clone");
        
#if !AIRSHIP_PLAYER
        [DidReloadScripts]
        public static void OnScriptReload() {
            EditorCoroutines.Execute(ResumeOrStartTypescriptRuntime());
        }

        private static WebsocketClient client;
        [MenuItem("Airship/TypeScript/Connect...")]
        public static void Connect() {
            client = new WebsocketClient(new Uri("ws://localhost:7472"));
            client.WebsocketReady += () => {
                client.CompileAllFiles();
            };
            client.WebsocketDataReceived += data => {
                Debug.Log($"Received: {data}");
            };
            _ = client.ConnectAsync();
        }

        [MenuItem("Airship/TypeScript/Disconnect")]
        public static void Disconnect() {
            _ = client.CloseAsync();
        }
        
        
        [InitializeOnLoadMethod]
        public static void OnLoad() {
            // If a server or clone - ignore
            if (!IsValidEditor) return;

            if (!SessionState.GetBool("TSSLoadedUnityEditor", false)) {
                SessionState.SetBool("TSSLoadedUnityEditor", true);
                EditorApplication.delayCall += OnLoadDeferred;
                EditorApplication.playModeStateChanged += PlayModeStateChanged;
                EditorApplication.quitting += OnEditorQuitting;
            }
        }
#endif

        private static void OnEditorQuitting() {
            // Stop any running compilers pls
            TypescriptCompilationService.StopCompilerServices();
        }

        private static void PlayModeStateChanged(PlayModeStateChange obj) {
            if (obj == PlayModeStateChange.EnteredPlayMode && TypescriptCompilationService.PreventPlayModeWithErrors) {
                if (TypescriptCompilationService.ErrorCount > 0) {
                    foreach( SceneView scene in SceneView.sceneViews ) {
                        scene.ShowNotification(new GUIContent("There are TypeScript compilation errors in your project"));
                    }

                    EditorApplication.isPlaying = false;
                }
            }

            // Require files compiled to go into play mode
            if (obj == PlayModeStateChange.ExitingEditMode && EditorApplication.isPlayingOrWillChangePlaymode && TypescriptCompilationService.IsCompilingFiles) {
                // We'll yield the editor to wait for those files to finish compiling before entering play mode...
                while (TypescriptCompilationService.IsCompilingFiles || TypescriptCompilationService.IsImportingFiles) {
                    var compilationState = TypescriptProjectsService.Project.CompilationState;
                    EditorUtility.DisplayProgressBar("Typescript Services", 
                        $"Finishing compilation of Typescript files ({compilationState.CompiledFileCount}/{compilationState.FilesToCompileCount})", 
                        (float) compilationState.CompiledFileCount / compilationState.FilesToCompileCount);
                    Thread.Sleep(10);
                }
                
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool HasAllPackagesDownloaded() {
            var gameConfig = GameConfig.Load();
            foreach (var project in gameConfig.packages) {
                if (!project.localSource && !project.IsDownloaded()) return false;
            }

            return true;
        }
        
        private static IEnumerator InitializeProject() {
            TypescriptProjectsService.ReloadProjects();
            TypescriptCompilationService.ClearIncrementalCache(); // clear incremental cache
            yield return new WaitUntil(HasAllPackagesDownloaded);
            yield return InitializeTypeScript();
            yield return StartTypescriptRuntime();
        }

        /// <summary>
        /// True if the compiler services is currently "restarting" due to something like packages updating
        /// </summary>
        internal static bool IsAwaitingRestart { get; private set; }
        
        internal static IEnumerator RestartAndAwaitUpdates() {
            if (!TypescriptCompilationService.IsWatchModeRunning || IsAwaitingRestart) {
                yield break;
            }
            
            IsAwaitingRestart = true;
            TypescriptCompilationService.StopCompilerServices();
            TypescriptCompilationService.ClearIncrementalCache();
            yield return new WaitUntil(() => !AirshipPackagesWindow.IsModifyingPackages && !AirshipUpdateService.IsUpdatingAirship);
            TypescriptCompilationService.StartCompilerServices();
            IsAwaitingRestart = false;
        }

        private static IEnumerator InitializeTypeScript() {
            TypescriptProjectsService.CheckTypescriptProject(); // ????
            yield return null;
        }

        private static IEnumerator StopTypescriptRuntime() {
            TypescriptCompilationService.StopCompilerServices();
            yield return null;
        }
        
        private  static IEnumerator StartTypescriptRuntime() {
            TypescriptProjectsService.ReloadProject();
            
            if (TypescriptCompilationService.IsWatchModeRunning) {
                TypescriptCompilationService.StopCompilerServices(true);
            } else {
                TypescriptCompilationService.StartCompilerServices();
            }

            yield break;
        }

        private static IEnumerator ResumeOrStartTypescriptRuntime() {
            if (!TypescriptCompilationService.IsWatchModeRunning) {
                yield return StartTypescriptRuntime();
            }
            else {
                var compilerState = TypescriptCompilationService.WatchStates[0];
                if (compilerState != null) {
                    var process = compilerState.CompilerProcess;
                    var processArguments = compilerState.compilerArguments;

                    if (process != null && process.HasExited == false) {
                        Debug.Log("[TypescriptServices] Detected Unity domain reload, reattaching compiler...");
                        // Reattach the output of the console
                        TypescriptCompilationService.AttachWatchOutputToUnityConsole(compilerState, processArguments, process, isDomainReload: true);
                    }
                    else {
                        Debug.LogWarning("[TypescriptServices] Could not reattach process, restarting compiler...");
                        TypescriptCompilationService.StopCompilerServices(shouldRestart: true); // ???????????????
                    }
                }
            }
        }

        private static void CheckForConsoleClear() {
            var logCount = LogExtensions.GetLogCount();
            if (logCount < TypescriptCompilationService.ErrorCount) {
                // If log count < errCount, assume cleared
            }
        }

        private static void OnLoadDeferred() {
            
            
            var project = TypescriptProjectsService.ReloadProject();
            if (project == null) {
                Debug.LogWarning($"Missing Typescript Project");
                TypescriptProjectsService.EnsureProjectConfigsExist();
                return;
            }

            project.EnforceDefaultConfigurationSettings();
            EditorApplication.delayCall -= OnLoadDeferred;
            EditorApplication.update += OnUpdate;

            CompilerCrash += OnCrash;

            // If offline, only start TSServices if initialized
            var offline = Application.internetReachability == NetworkReachability.NotReachable;
            if (offline) {
                var config = TypescriptServicesLocalConfig.instance;
                if (config.hasInitialized) {
                    EditorCoroutines.Execute(StartTypescriptRuntime());
                }
                
                return;
            }
            
            if (!SessionState.GetBool("InitializedTypescriptServices", false)) {
                SessionState.SetBool("InitializedTypescriptServices", true);
                TypescriptCompilationService.StopCompilerServices();
                
                var config = TypescriptServicesLocalConfig.instance;
                if (!config.hasInitialized) {
                    EditorCoroutines.Execute(InitializeProject(), (done) => {
                        if (!done) return;
                        config.hasInitialized = true;
                        config.Modify();
                    });
                }
                else {
                    EditorCoroutines.Execute(StartTypescriptRuntime());
                }
            }
            else {
                TypescriptCompilationService.StopCompilerServices(shouldRestart: TypescriptCompilationService.IsWatchModeRunning);
            }
        }

        private static void OnCrash(TypescriptCrashProblemItem problem) {
            var errorLog = problem.StandardError;
            if (errorLog.Count() >= 8) {
               EditorUtility.DisplayDialog("Typescript Compiler Crashed",
                        $"{string.Join("\n", problem.StandardError.ToArray()[4..7])}",
                        "Ok");
            }else {
                if (EditorUtility.DisplayDialog("Typescript Compiler quit unexpectedly...",
                        $"{problem.Message} - check the Typescript Console for more details.",
                        "Restart...", "Ok")) {
                    EditorCoroutines.Execute(StartTypescriptRuntime());
                }
            }
            

        }

        private static IEnumerator RestoreErrorsOnNextFrame() {
            yield return new WaitForEndOfFrame();
            
            var prefix = $"<color=#8e8e8e>TS</color>";
            
            foreach (var problem in TypescriptProjectsService.Project.ProblemItems) {
                if (problem is TypescriptFileDiagnosticItem diagnosticItem) {
                    var diagnosticString = ConsoleFormatting.GetProblemItemString(diagnosticItem);
                    Debug.LogError($"{prefix} {diagnosticString}");
                }
                   
            }

            isRestoringErrors = false;
        }
        
        private static int prevLogCount = 0;
        private static bool isRestoringErrors = false;
        private static bool invokedCrashEvent = false;
        
        private static void OnUpdate() {
            if (isRestoringErrors) return;
            int logCount = LogExtensions.GetLogCount();
            
            if (logCount <= 0 && TypescriptProjectsService.ProblemCount > 0 && EditorIntegrationsConfig.instance.typescriptRestoreConsoleErrors) {
                
                // Assume it was cleared
                isRestoringErrors = true;
                EditorCoroutines.Execute(RestoreErrorsOnNextFrame());
            }

            if (TypescriptCompilationService.Crashed && !invokedCrashEvent) {
                invokedCrashEvent = true;
                CompilerCrash?.Invoke(TypescriptProjectsService.Project.CrashProblemItem);
            }
            else if (!TypescriptCompilationService.Crashed) {
                invokedCrashEvent = false;
            }
        }
    }
}
#endif