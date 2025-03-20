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
        private static Dictionary<AirshipScript, HashSet<string>> scriptToPrefabs = new();
        
        private void OnPostprocessPrefab(GameObject g) {
            var isApplyingChanges = TypescriptCompilationService.IsCurrentlyCompiling;
            Debug.LogWarning($"PostProcess Prefab {isApplyingChanges}");
            
            // var behaviours = g.GetComponentsInChildren<AirshipComponent>();
            // foreach (var behaviour in behaviours) {
            //     behaviour.ReconcileMetadata();
            // }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        private static void LinkScriptsToPrefabs(string[] paths) {
            foreach (var path in paths) {
                if (!path.EndsWith(".prefab", StringComparison.InvariantCulture)) continue;
                var scripts = new HashSet<AirshipScript>();
                
                var components = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var component in components.GetComponentsInChildren<AirshipComponent>()) {
                    scripts.Add(component.script);
                }
                
                foreach (var script in scripts)
                {
                    if (!scriptToPrefabs.TryGetValue(script, out var assetPaths)) {
                        assetPaths = new HashSet<string>();
                        scriptToPrefabs.Add(script, assetPaths);
                    }

                    assetPaths.Add(script.assetPath);
                    Debug.Log($"Add [ {string.Join(", ", assetPaths)} ] for {path}");
                }
            }
        }

        private static void UnlinkScriptsFromPrefabs(string[] paths) {
            foreach (var path in paths) {
                if (!path.EndsWith(".prefab", StringComparison.InvariantCulture)) continue;
                foreach (var pathSet in scriptToPrefabs) {
                    if (pathSet.Value.Contains(path)) {
                        Debug.Log($"Remove prefab {path} from linked script {pathSet.Key}");
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
            if (scriptToPrefabs.TryGetValue(script, out var prefabs)) {
                return prefabs;
            }
            else {
                return new string[] { };
            }
        }
    }
}