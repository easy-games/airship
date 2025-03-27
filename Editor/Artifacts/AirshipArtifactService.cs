#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal static class AirshipArtifactService {
        private static Dictionary<string, HashSet<AirshipComponent>> reconcileList = new();
        
        [InitializeOnLoadMethod]
        public static void OnLoad() {
            AirshipComponent.Reconcile += OnComponentReconcile;
        }

        /// <summary>
        /// Called when a component wants to queue reconciliation
        /// </summary>
        public static void OnComponentQueueReconcile(AirshipComponent component) {
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
        public static void OnComponentDestroyed(AirshipComponent component) {
            // Obviously we'd want the artifact data destroyed at this point, it's useless to at that point.
            
            var db = AirshipLocalArtifactDatabase.instance;
            db.RemoveComponentData(component);
        }

        /// <summary>
        /// Reconciles all queued components for the given script
        /// </summary>
        public static void ReconcileQueuedComponents(AirshipScript script) {
            // So the idea of this is to reconcile the components AFTER scripts are in the artifact db
            // ... then we can force a reconciliation at that point to ensure the data is up-to-date.
            
            if (!reconcileList.TryGetValue(script.assetPath, out var componentSet)) return;
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
        }
        
#if AIRSHIP_DEBUG
        [MenuItem("Airship/Wipe Editor Data")]
        public static void Wipe() {
            var instance = AirshipLocalArtifactDatabase.instance;
            instance.scripts = new List<ScriptAssetData>();
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
            var instance = AirshipLocalArtifactDatabase.instance;
            
            // If we have no script data, or it's not got metadata
            // we should probably skip reconciliation at this stage
            var hasData = instance.TryGetScriptAssetData(component.script, out var scriptData);
            if (!hasData) {
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] Script not yet existing, deferring reconciliation for {eventData.Component.guid}...");
#endif
                OnComponentQueueReconcile(component);
                eventData.ShouldReconcile = false;
                return;
            }
            
            var components = instance.components;
            var componentData = components.FirstOrDefault(f => component.guid == f.guid);
            if (componentData == null) {
                componentData  = new ComponentData() {
                    guid = component.guid,
                    scriptPath = component.script.assetPath,
                };
                components.Add(componentData);
                instance.Modify();
            }
            
            // If we have metadata, and it's older than the script data we
            // force a reconcile
            if (scriptData.IsNewerThan(componentData) || scriptData.IsOutOfSyncWith(componentData)) {
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] Reconciliation for {componentData.guid} ({scriptData.assetPath})");
#endif
                component.hash = component.script.compiledFileHash;
                componentData.metadata = scriptData.metadata.Clone();
                instance.Modify();
            }
            else {
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] skip reconcile for {componentData.guid} ({scriptData.assetPath})");
#endif
                eventData.ShouldReconcile = false;
            }
        }
    }
}
#endif