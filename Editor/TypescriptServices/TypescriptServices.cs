#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Editor;
using Editor.EditorInternal;
using Editor.Packages;
using ParrelSync;
using Unity.EditorCoroutines.Editor;
using Unity.Multiplayer.Playmode;
using UnityEditor;
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
    
    /// <summary>
    /// Main static class for handling the TypeScript services
    /// </summary>
    public static class TypescriptServices {
        /// <summary>
        /// Returns true if this is a valid editor window to run TSS in
        /// </summary>
        public static bool IsValidEditor =>
            !ClonesManager.IsClone() &&
            !Environment.GetCommandLineArgs().Contains("--virtual-project-clone");

        [InitializeOnLoadMethod]
        public static void OnLoad() {
#if AIRSHIP_PLAYER
            Debug.LogWarning("[TypescriptServices] Skipped, in Airship Player mode");
            return;
#endif
            // If a server or clone - ignore
            if (!IsValidEditor) return;
            EditorApplication.delayCall += OnLoadDeferred;

            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
        }

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
            TypescriptProjectsService.CheckTypescriptProject(); // ??
            yield return null;
        }

        private static IEnumerator StopTypescriptRuntime() {
            TypescriptCompilationService.StopCompilerServices();
            yield return null;
        }
        
        private  static IEnumerator StartTypescriptRuntime() {
            TypescriptProjectsService.ReloadProject();
            
            // if (!EditorIntegrationsConfig.instance.typescriptAutostartCompiler) yield break;

            if (TypescriptCompilationService.IsWatchModeRunning) {
                TypescriptCompilationService.StopCompilerServices(true);
            } else {
                TypescriptCompilationService.StartCompilerServices();
            }

            yield break;
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
        private static void OnUpdate() {
            if (isRestoringErrors) return;
            int logCount = LogExtensions.GetLogCount();
            
            if (logCount <= 0 && TypescriptProjectsService.ProblemCount > 0 && EditorIntegrationsConfig.instance.typescriptRestoreConsoleErrors) {
                
                // Assume it was cleared
                isRestoringErrors = true;
                EditorCoroutines.Execute(RestoreErrorsOnNextFrame());
            }
        }
    }
}
#endif