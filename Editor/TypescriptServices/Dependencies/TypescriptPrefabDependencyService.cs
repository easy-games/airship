using System;
using System.Collections.Generic;
using System.Linq;
using Editor.EditorInternal;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Airship.Editor {
    public class TypescriptPrefabDependencyService : AssetPostprocessor {
        private static Dictionary<string, HashSet<string>> scriptToPrefabs = new();
        private static Dictionary<string, HashSet<string>> prefabsToScripts = new();

        [InitializeOnLoadMethod]
        public static void OnLoad() {
            scriptToPrefabs.Clear();
            prefabsToScripts.Clear();
        }

        [MenuItem("Airship/Debug/Print Script To Prefabs")]
        public static void PrintScriptToPrefabs() {
            Debug.Log("scriptsToPrefabs:");
            foreach (var scriptToPrefab in scriptToPrefabs) {
                Debug.Log($"\t{scriptToPrefab.Key}: {string.Join(", ", scriptToPrefab.Value)}");
            }
            
            Debug.Log("prefabsToScripts:");
            foreach (var prefabToScripts in prefabsToScripts) {
                Debug.Log($"\t{prefabToScripts.Key}: {string.Join(", ", prefabToScripts.Value)}");
            }
            //
            // Debug.Log("all");
            // var components = TypescriptProjectsService.GetAllAirshipComponentsInPrefabs();
            // Debug.Log($"{components.Count} airship components in project");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        private static void LinkScriptsToPrefabs(string[] paths) {
            foreach (var path in paths) {
                if (!path.EndsWith(".prefab", StringComparison.InvariantCulture)) continue;
                
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var components = prefab.GetComponentsInChildren<AirshipComponent>();
                if (components.Length == 0) continue;

                // if (!prefabsToScripts.TryGetValue(path, out var prevScripts)) {
                //     prevScripts = new HashSet<string>();
                // }
                //
                // var scriptsToAdd = new HashSet<string>();
                //
                // foreach (var component in components) {
                //     if (!component.script) continue;
                //     scriptsToAdd.Add(component.script.assetPath);
                // }
                //
                // // remove old scripts
                // foreach (var prevScript in prevScripts.ToArray()) {
                //     if (scriptsToAdd.Contains(prevScript)) continue;
                //     
                //     if (scriptToPrefabs.TryGetValue(prevScript, out var linkedAssets)) {
                //         linkedAssets.Remove(path);
                //     }
                //     
                //     prevScripts.Remove(prevScript);
                // }
                //
                // if (!prefabsToScripts.TryGetValue(path, out var linkedScripts)) {
                //     linkedScripts = new HashSet<string>();
                //     prefabsToScripts.Add(path, linkedScripts);
                // }
                //
                // foreach (var script in scriptsToAdd) {
                //     if (linkedScripts.Contains(script)) continue;
                //     linkedScripts.Add(script);
                //
                //     if (!scriptToPrefabs.TryGetValue(script, out var prefabPaths)) {
                //         prefabPaths = new HashSet<string>();
                //         scriptToPrefabs.Add(script, prefabPaths);
                //     }
                //
                //     prefabPaths.Add(path);
                // }
            }
        }

        private static void UnlinkScriptsFromPrefabs(string[] paths) {
            foreach (var path in paths) {
                if (!path.EndsWith(".prefab", StringComparison.InvariantCulture)) continue;
                foreach (var pathSet in scriptToPrefabs) {
                    if (pathSet.Value.Contains(path)) {
                    }
                }
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {
            LinkScriptsToPrefabs(importedAssets);
            UnlinkScriptsFromPrefabs(deletedAssets);
        }

        internal static IEnumerable<string> GetDependentAssetsForScript(AirshipScript script) {
            if (scriptToPrefabs.TryGetValue(script.assetPath, out var prefabs)) {
                return prefabs;
            }
            else {
                return new string[] { };
            }
        }

        internal static bool HasReconciliationDependencies(AirshipScript script) {
            return scriptToPrefabs.TryGetValue(script.assetPath, out _);
        }
        
        internal static bool ReconcileIfPostCompile(AirshipScript script) {
            if (TypescriptCompilationService.CompilerState != TypescriptCompilerState.PostCompile) return false;
            if (script.m_metadata == null) return false;
            if (!scriptToPrefabs.TryGetValue(script.assetPath, out var prefabs)) return false;

            foreach (var prefab in prefabs) {
                var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (!prefabObject) continue;
                
                foreach (var component in prefabObject.GetComponentsInChildren<AirshipComponent>()) {
                    if (!component.script || component.script.assetPath != script.assetPath) continue;
                    Debug.Log($"Reconcile {component.script.assetPath} for {prefab} due to post-compile");
                    component.ReconcileMetadata(ReconcileSource.PostCompile, script.m_metadata);
                }
            }

            return true;
        }

        internal static void RemovePrefabDependencies(AirshipScript script) {
            scriptToPrefabs.Remove(script.assetPath);
            foreach (var prefabToScript in prefabsToScripts) {
                if (!prefabToScript.Value.Contains(script.assetPath)) continue;
                prefabToScript.Value.Remove(script.assetPath);
            }
        }
    }
}