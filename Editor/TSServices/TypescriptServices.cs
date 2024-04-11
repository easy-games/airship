using System.Collections;
using Editor.Packages;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    [FilePath("Library/TypescriptServices", FilePathAttribute.Location.ProjectFolder)]
    internal class TypescriptServicesLocalConfig : ScriptableSingleton<TypescriptServicesLocalConfig> {
        [SerializeField]
        internal bool hasInitialized = false;

        /// <summary>
        /// Whether or not the local typescript services have been initialized
        /// </summary>
        public bool Initialized => hasInitialized;

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
        }
        
        private static bool HasAllPackagesDownloaded() {
            var gameConfig = GameConfig.Load();
            foreach (var project in gameConfig.packages) {
                if (!project.localSource && !project.IsDownloaded()) return false;
            }

            return true;
        }
        
        private static IEnumerator InitializeProject() {
            yield return new WaitUntil(HasAllPackagesDownloaded);
            yield return InitializeTypeScript();
            yield return StartTypescriptRuntime();
        }

        private static IEnumerator InitializeTypeScript() {
            TypescriptProjectsService.UpdateTypescript(); // ??
            yield break;
        }
        
        private  static IEnumerator StartTypescriptRuntime() {
            if (!EditorIntegrationsConfig.instance.typescriptAutostartCompiler) yield break;

            if (TypescriptCompilationService.IsWatchModeRunning) {
                TypescriptCompilationService.StopCompilerServices();
                yield return new WaitForSeconds(2);
                TypescriptCompilationService.StartCompilerServices();
            }
            else {
                TypescriptCompilationService.StartCompilerServices();
            }

            yield break;
        }

        private static void OnLoadDeferred() {
            EditorApplication.delayCall -= OnLoadDeferred;
            
            var config = TypescriptServicesLocalConfig.instance;
            if (!config.Initialized) {
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