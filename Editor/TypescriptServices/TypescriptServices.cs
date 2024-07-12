﻿using System.Collections;
using System.Linq;
using Editor;
using Editor.Packages;
using ParrelSync;
using Unity.Multiplayer.Playmode;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [FilePath("Library/TypescriptServices", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptServicesLocalConfig : ScriptableSingleton<TypescriptServicesLocalConfig> {
        [SerializeField]
        internal bool hasInitialized = false;
        
        public void Modify() {
            Save(true);
        }
    }
    
    /// <summary>
    /// Main static class for handling the TypeScript services
    /// </summary>
    public static class TypescriptServices {
        [InitializeOnLoadMethod]
        public static void OnLoad() {
            var tags = CurrentPlayer.ReadOnlyTags();
            var isCloneOrMPPM = tags.Length > 0 || ClonesManager.IsClone();
            
            // If a server or clone - ignore
            if (isCloneOrMPPM) return;
            EditorApplication.delayCall += OnLoadDeferred;

            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting() {
            // Stop any running compilers pls
            TypescriptCompilationService.StopCompilerServices();
        }

        private static void PlayModeStateChanged(PlayModeStateChange obj) {
            if (obj == PlayModeStateChange.EnteredPlayMode && EditorIntegrationsConfig.instance.typescriptPreventPlayOnError) {
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
            yield return new WaitUntil(HasAllPackagesDownloaded);
            yield return InitializeTypeScript();
            yield return StartTypescriptRuntime();
        }

        /// <summary>
        /// True if the compiler services is currently "restarting" due to something like packages updating
        /// </summary>
        internal static bool IsAwaitingRestart { get; private set; }

        internal static IEnumerator RestartTypescriptRuntimeForPackageUpdates() {
            if (!TypescriptCompilationService.IsWatchModeRunning || IsAwaitingRestart) {
                yield break;
            }
            
            IsAwaitingRestart = true;
            TypescriptCompilationService.StopCompilerServices();
            TypescriptProjectsService.ReloadProject();
            yield return new WaitUntil(() => !AirshipPackagesWindow.IsDownloadingPackages);
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
            
            if (!EditorIntegrationsConfig.instance.typescriptAutostartCompiler) yield break;

            if (TypescriptCompilationService.IsWatchModeRunning) {
                TypescriptCompilationService.StopCompilerServices(true);
            } else {
                TypescriptCompilationService.StartCompilerServices();
            }
        }

        private static void OnLoadDeferred() {
            var project = TypescriptProjectsService.ReloadProject();
            if (project == null) {
                return;
            }

            project.EnforceDefaultConfigurationSettings();
            
            EditorApplication.delayCall -= OnLoadDeferred;
            
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
    }
}