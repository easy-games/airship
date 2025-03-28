#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    /// <summary>
    /// This handles things such as component/script states in Airship's Editor
    /// </summary>
    internal static class AirshipArtifactService {
        private static Dictionary<string, HashSet<AirshipComponent>> reconcileList = new();
        private static IEnumerable<AirshipComponent> _airshipComponentCache;
        
        [InitializeOnLoadMethod]
        internal static void OnLoad() {
            AirshipComponent.Reconcile += OnComponentReconcile;
        }
 
        internal static void StartScriptUpdates(List<string> scriptPaths) {
            var items = Resources.FindObjectsOfTypeAll<AirshipComponent>();
            var modifyCache = new List<AirshipComponent>();
            
            foreach (var item in items) {
                if (item.script == null || !scriptPaths.Contains(item.script.assetPath)) continue;
                modifyCache.Add(item);
            }

            _airshipComponentCache = modifyCache;
        }

        internal static void StopScriptUpdates() {
            _airshipComponentCache = null;
        }
        
        /// <summary>
        /// Called when a component wants to queue reconciliation
        /// </summary>
        internal static void OnComponentQueueReconcile(AirshipComponent component) {
            // We add the component to a reconcile list attached to the given script path
            // Then later, we'll reconcile for all components based on script target
            
            if (!reconcileList.TryGetValue(component.script.assetPath, out var componentSet)) {
                componentSet = new HashSet<AirshipComponent>();
                reconcileList.Add(component.script.assetPath, componentSet);
            }

            componentSet.Add(component);
        }

        /// <summary>
        /// Called when a component is destroyed by the user
        /// </summary>
        internal static void OnComponentDestroyed(AirshipComponent component) {
            // Obviously we'd want the artifact data destroyed at this point, it's useless to at that point.
            
            var db = AirshipLocalArtifactDatabase.instance;
            db.RemoveComponentData(component);
        }

        /// <summary>
        /// Reconciles all dependencies of the given script
        /// </summary>
        /// <param name="script">The script to reconcile dependencies for</param>
        internal static void ReconcileImportDependencies(AirshipScript script) {
            foreach (var component in _airshipComponentCache) {
                if (component.script.assetPath != script.assetPath) continue;
                component.ReconcileMetadata(ReconcileSource.ForceReconcile, script.m_metadata);
            }
        }
        
        /// <summary>
        /// Reconciles all queued components for the given script
        /// </summary>
        /// <param name="script">The script of the queued components to reconcile</param>
        /// <returns>True if there were components that were reconciled</returns>
        internal static bool ReconcileQueuedComponents(AirshipScript script) {
            // So the idea of this is to reconcile the components AFTER scripts are in the artifact db
            // ... then we can force a reconciliation at that point to ensure the data is up-to-date.
            
            if (!reconcileList.TryGetValue(script.assetPath, out var componentSet)) return false;
            foreach (var component in componentSet) {
                if (!component) {
                    Debug.LogWarning($"Could not reconcile component");
                    continue;
                }
                
#if AIRSHIP_DEBUG
                Debug.Log($"[ReconcileDependents] Reconcile {component.guid} ({script.assetPath})");
#endif
                component.ReconcileMetadata(ReconcileSource.ForceReconcile, script.m_metadata);
            }
            
#if AIRSHIP_DEBUG
            Debug.Log($"[ReconcileDependents] Reconciled for all {reconcileList.Count} dependencies of {script.assetPath}");
#endif
            reconcileList.Remove(script.assetPath);
            return true;
        }
        
#if AIRSHIP_DEBUG
        [MenuItem("Airship/Wipe Editor Data")]
        internal static void Wipe() {
            var instance = AirshipLocalArtifactDatabase.instance;
            instance.scripts = new List<ComponentScriptAssetData>();
            instance.components = new List<ComponentData>();
            instance.Modify();
        }
#endif

        /// <summary>
        /// Callback for when a component is requesting reconciliation
        /// </summary>
        private static void OnComponentReconcile(AirshipReconcileEventData eventData) {
            var component = eventData.Component;
            if (!component.script) return;
            var artifactData = AirshipLocalArtifactDatabase.instance;

            // If we have no script data, or it's not got metadata
            // we should probably skip reconciliation at this stage
            var hasData = artifactData.TryGetScriptAssetData(component.script, out var scriptData);
            if (!hasData) {
#if AIRSHIP_DEBUG
                Debug.Log(
                    $"[Reconcile] Script not yet existing, deferring reconciliation for {eventData.Component.guid}...");
#endif
                OnComponentQueueReconcile(component);
                eventData.ShouldReconcile = false;
                return;
            }

            var components = artifactData.components;
            var componentData = components.FirstOrDefault(f => component.guid == f.guid);
            if (componentData == null) {
                componentData = new ComponentData() {
                    guid = component.guid,
                    scriptPath = component.script.assetPath,
                };
                components.Add(componentData);
                artifactData.Modify();
            }
            
            // If the component is different to the script, we should probably invoke a reconcile anyway
            if (scriptData.IsMismatchedWithComponent(component)) {
#if AIRSHIP_DEBUG
                Debug.Log(
                    $"[Reconcile] Discrepancy detected for {eventData.Component.guid}... reconcile queued for next script compilation cycle...");
#endif
                OnComponentQueueReconcile(component);
                eventData.ShouldReconcile = false;

#if AIRSHIP_DEBUG
                // If we're running, but idle maybe we should note this
                if (TypescriptCompilationService.IsWatchModeRunning && TypescriptCompilationService.CompilerState != TypescriptCompilerState.Idle) {
                    Debug.Log("[Reconcile] WatchMode + Idle, should it force recompile state?");
                }
#endif
                return;
            }
            
            // If the script's newer, or the hash is equal we can safely reconcile
            if (scriptData.IsNewerThan(componentData) || scriptData.HasSameHashAs(componentData)) {
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] Reconciliation for {componentData.guid} ({scriptData.assetPath})");
#endif
                component.hash = component.script.compiledFileHash;
                componentData.metadata = scriptData.metadata.Clone();
                artifactData.Modify();
            }
            else {
                // It's unsafe to reconcile this data... sorry!
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] skip reconcile for {componentData.guid} ({scriptData.assetPath})");
#endif
                eventData.ShouldReconcile = false;
            }
        }
    }
}
#endif