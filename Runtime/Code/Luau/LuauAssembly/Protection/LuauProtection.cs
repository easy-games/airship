using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Luau.LuauAssembly.Protection {
    public class LuauProtection {
        /// The Luau context from the most recent call from the Luau plugin.
        public static LuauContext CurrentContext = LuauContext.Game;
        
        private static readonly string[] protectedScenesNames = {
            "corescene", "mainmenu", "login", "disconnected", "airshipupdateapp", "dontdestroyonload",
        };
        private static HashSet<int> protectedSceneHandles = new HashSet<int>();
        
        public static bool IsProtectedScene(Scene scene) {
            return protectedSceneHandles.Contains(scene.handle);
        }
        
        public static void SetupProtectedSceneHandleListener() {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                RegisterPossiblyProtectedScene(scene);
            }

            SceneManager.sceneLoaded += (scene, mode) => {
                RegisterPossiblyProtectedScene(scene);
            };
            SceneManager.sceneUnloaded += scene => {
                protectedSceneHandles.Remove(scene.handle);
            };
        }

        /// <summary>
        /// Unless you only have scene name you should use IsProtectedScene
        /// </summary>
        public static bool IsProtectedSceneName(string sceneName) {
            if (string.IsNullOrEmpty(sceneName)) return false;

            foreach (var protectedSceneName in protectedScenesNames) {
                if (protectedSceneName.Equals(sceneName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsAccessBlocked(LuauContext context, UnityEngine.Object unityObject) {
            if (unityObject is GameObject go) {
                return IsAccessBlocked(context, go);
            }
            if (unityObject is Transform transform) {
                return IsAccessBlocked(context, transform.gameObject);
            }
            if (unityObject is Component component) {
                return IsAccessBlocked(context, component.gameObject);
            }
            return false;
        }
        
        public static bool IsAccessBlocked(LuauContext context, GameObject gameObject) {
            if (gameObject == null) return false;
            if (context != LuauContext.Protected && IsProtectedScene(gameObject.scene)) {
                var parent = gameObject.transform.parent;
                if (parent?.name is "GameReadAccess" || parent?.parent?.name is "GameReadAccess") {
                    return false;
                }

                return true;
            }

            return false;
        }
        
        private static void RegisterPossiblyProtectedScene(Scene scene) {
            if (IsProtectedSceneName(scene.name)) {
                protectedSceneHandles.Add(scene.handle);
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnReload() {
            protectedSceneHandles.Clear();
        }
    }
}