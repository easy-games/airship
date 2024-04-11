using System.Collections;
using Editor.Packages;
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
            if (RunCore.IsClone()) return;
            EditorApplication.delayCall += OnLoadDeferred;

            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        private static void PlayModeStateChanged(PlayModeStateChange obj) {
            if (obj == PlayModeStateChange.EnteredPlayMode && EditorIntegrationsConfig.instance.typescriptPreventPlayOnError) {
                if (TypescriptCompilationService.ErrorCount > 0) {
                    foreach( SceneView scene in SceneView.sceneViews ) {
                        scene.ShowNotification(new GUIContent("There are TypeScript compilation errors in your project"));
                    }

                    EditorApplication.isPlaying = false;
                } else if (TypescriptCompilationService.IsCurrentlyCompiling) {
                    foreach( SceneView scene in SceneView.sceneViews ) {
                        scene.ShowNotification(new GUIContent("One or more project(s) are still compiling!"));
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

        private static IEnumerator InitializeTypeScript() {
            TypescriptProjectsService.UpdateTypescript(); // ??
            yield break;
        }
        
        private  static IEnumerator StartTypescriptRuntime() {
            var config = TypescriptServicesLocalConfig.instance;
            TypescriptProjectsService.ReloadProjects();
            
            if (!EditorIntegrationsConfig.instance.typescriptAutostartCompiler) yield break;

            if (TypescriptCompilationService.IsWatchModeRunning) {
                TypescriptCompilationService.StopCompilerServices();
                yield return new WaitForSeconds(2);
                TypescriptCompilationService.StartCompilerServices();
            }
            else if (!config.hasInitialized) {
                TypescriptCompilationService.StartCompilerServices();
            }

            yield break;
        }

        private static void OnLoadDeferred() {
            EditorApplication.delayCall -= OnLoadDeferred;
            
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
    }
}