#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Luau;
using Mirror.SimpleWeb;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    /// <summary>
    /// This handles things such as component/script states in Airship's Editor
    /// </summary>
    internal static class AirshipArtifactService {
        public static ReconcilerVersion DefaultReconcilerVersion => ReconcilerVersion.Version1;
        public static ReconcilerVersion ReconcilerVersion {
            get {
                if (EditorIntegrationsConfig.instance.useProjectReconcileOption) {
                    var version = EditorIntegrationsConfig.instance.projectReconcilerVersion;
                    return version == ReconcilerVersion.Default ? DefaultReconcilerVersion : version;
                }
                else {
                    var version = AirshipLocalArtifactDatabase.instance.reconcilerVersion;
                    return version == ReconcilerVersion.Default ? DefaultReconcilerVersion : version;
                }
            }
            set {
                if (EditorIntegrationsConfig.instance.useProjectReconcileOption) {
                    EditorIntegrationsConfig.instance.projectReconcilerVersion = value;
                }
                else {
                    AirshipLocalArtifactDatabase.instance.reconcilerVersion = value;
                    EditorIntegrationsConfig.instance.projectReconcilerVersion = ReconcilerVersion.Default;
                }
            }
        }
        
        private static Dictionary<string, HashSet<AirshipComponent>> reconcileList = new();
        private static IEnumerable<AirshipComponent> _airshipComponentCache;
        
        [InitializeOnLoadMethod]
        internal static void OnLoad() {
            AirshipComponent.Reconcile += OnComponentReconcile;
        }
 
        internal static void StartScriptUpdates() {}

        internal static void StopScriptUpdates() {
            AirshipLocalArtifactDatabase.instance.Modify();
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
        /// Reconcile the component
        /// </summary>
        /// <param name="component"></param>
        internal static bool ReconcileComponent(AirshipComponent component) {
            // if (string.IsNullOrEmpty(component.guid)) {
            //     component.guid = Guid.NewGuid().ToString();
            // }
            
#if AIRSHIP_DEBUG
            var additions = new HashSet<string>();
            var deletions = new HashSet<string>();
            var modifications = new HashSet<string>();
#endif
            if (component.script == null) return false;
            

            if (component.script.m_metadata == null) return false;
            
            
            var scriptMetadata = component.script.m_metadata;
            var componentMetadata = component.metadata;

            if (scriptMetadata == null) return false;
            
            // Add missing properties
            foreach (var scriptProperty in scriptMetadata.properties) {
                var componentProperty = componentMetadata.FindProperty(scriptProperty.name);
                if (componentProperty == null) {
                    var element = scriptProperty.Clone();
                    componentMetadata.properties.Add(element);
                    componentProperty = element;
#if AIRSHIP_DEBUG
                    additions.Add(element.name);
#endif
                }
                else {
                    if (!componentProperty.HasSameTypesAs(scriptProperty)) {
                        componentProperty.ReconcileTypesWith(scriptProperty);
                        componentProperty.ReconcileItemsWith(scriptProperty);
#if AIRSHIP_DEBUG
                        modifications.Add(componentProperty.name);
#endif
                    }
                }
                
                componentProperty.fileRef = scriptProperty.fileRef;
                componentProperty.refPath = scriptProperty.refPath;
            }
            
            List<LuauMetadataProperty> propertiesToRemove = null;
            var seenProperties = new HashSet<string>();
            foreach (var componentProperty in componentMetadata.properties) {
                var scriptProperty = scriptMetadata.FindProperty(componentProperty.name);
                
                if (scriptProperty == null || seenProperties.Contains(componentProperty.name)) {
                    if (propertiesToRemove == null) {
                        propertiesToRemove = new List<LuauMetadataProperty>();
                    }
                    propertiesToRemove.Add(componentProperty);
                }
                
                seenProperties.Add(componentProperty.name);
            }

            if (propertiesToRemove != null) {
                foreach (var componentProperty in propertiesToRemove) {
#if AIRSHIP_DEBUG
                    deletions.Add(componentProperty.name);
#endif
                    componentMetadata.properties.Remove(componentProperty);
                }
            }


#if AIRSHIP_DEBUG
            if (additions.Count > 0 || modifications.Count > 0 || deletions.Count > 0) {
                Debug.Log($"<color=#b878f7>[Reconcile] ReconcileComponent(com) for '{component.name}'#{component.script.m_metadata?.name} - {additions.Count} adds, {modifications.Count} mods, {deletions.Count} deletions</color>");

                foreach (var addition in additions) {
                    Debug.Log($"\t<color=#78f798>+ {addition}</color>");
                }
	        
                foreach (var modification in modifications) {
                    Debug.Log($"\t<color=#f7f778>~ {modification}</color>");
                }
	        
                foreach (var deletion in deletions) {
                    Debug.Log($"\t<color=#f77878>- {deletion}</color>");
                }
            }
#endif
            return true;
        }
        
        /// <summary>
        /// Reconciles all queued components for the given script
        /// </summary>
        /// <param name="script">The script of the queued components to reconcile</param>
        /// <returns>True if there were components that were reconciled</returns>
        internal static bool ReconcileQueuedComponents(AirshipScript script) {
            // So the idea of this is to reconcile the components AFTER scripts are in the artifact db
            // ... then we can force a reconciliation at that point to ensure the data is up-to-date.

            if (script == null) {
                Debug.LogError("script is null in ReconcileQueuedComponents ??");
                return false;
            }
            
            if (!reconcileList.TryGetValue(script.assetPath, out var componentSet)) return false;
            foreach (var component in componentSet) {
                if (!component) {
                    Debug.LogWarning($"Could not reconcile component with path {script.assetPath}, it's now null for whatever reason... ?");
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

        /// <summary>
        /// Callback for when a component is requesting reconciliation
        /// </summary>
        private static void OnComponentReconcile(AirshipReconcileEventData eventData) {
            if (ReconcilerVersion != ReconcilerVersion.Version2) return;
            eventData.UseLegacyReconcile = false;
            
            if (string.IsNullOrEmpty(eventData.Component.guid)) {
                eventData.Component.guid = Guid.NewGuid().ToString();
            }

            var originalComponent = PrefabUtility.GetCorrespondingObjectFromSource(eventData.Component);
            var componentToReconcile = eventData.Component;
            if (!componentToReconcile.script) return;
            var metadata = componentToReconcile.script.m_metadata;
            if (metadata == null) return;
            
            var artifactData = AirshipLocalArtifactDatabase.instance;
            
            // If we have no script data, or it's not got metadata
            // we should probably skip reconciliation at this stage
            var hasData = artifactData.TryGetScriptAssetData(componentToReconcile.script, out var scriptData);
            if (!hasData) {
#if AIRSHIP_DEBUG
                Debug.Log(
                    $"[Reconcile] Script not yet existing, deferring reconciliation for {eventData.Component.guid}...");
#endif
                OnComponentQueueReconcile(componentToReconcile);
                eventData.ShouldReconcile = false;
                return;
            }

            var components = artifactData.components;
            var componentData = components.FirstOrDefault(f => componentToReconcile.guid == f.guid);
            if (componentData == null) {
                componentData = new ComponentData() {
                    guid = componentToReconcile.guid,
                    script = componentToReconcile.script.assetPath,
                };
                components.Add(componentData);
                artifactData.Modify();
            }
            
            // If locally we've got the same hash, and yet the component is different... we can assume the prefab is newer but our database hasn't caught up!
            if (scriptData.HasSameHashAs(componentData) && scriptData.IsNotSameHashAsComponent(componentToReconcile)) {
#if AIRSHIP_DEBUG
                Debug.Log(
                    $"[Reconcile] Discrepancy detected for {eventData.Component.guid}... reconcile queued for next script compilation cycle...");
#endif
                OnComponentQueueReconcile(componentToReconcile);
                eventData.ShouldReconcile = false;
                return;
            }

            if (componentToReconcile.hash == null) {
                componentToReconcile.hash = componentToReconcile.script.sourceFileHash;
            }
            
            // If the script's newer, or the hash is equal we can safely reconcile
            if (scriptData.IsNewerThan(componentData) || scriptData.HasSameHashAs(componentData)) {
                componentToReconcile.metadata.name = metadata.name;
                
                // Reconcile the original component + this instance of it
                if (originalComponent) {
                    originalComponent.hash = componentToReconcile.script.sourceFileHash;
                    if (!ReconcileComponent(originalComponent)) { // If can't reconcile original, skip out the process 
#if AIRSHIP_DEBUG
                        Debug.LogWarning("[Reconcile] Failed reconcile prefab component, will skip instance prefab reconciliation until later.");
#endif
                        return;
                    }
                }
                
                componentToReconcile.hash = componentToReconcile.script.sourceFileHash;
                if (!ReconcileComponent(componentToReconcile)) {
#if AIRSHIP_DEBUG
                    Debug.LogWarning("[Reconcile] Failed to reconcile instance component");
#endif
                    return;
                }
             
                componentData.metadata = scriptData.metadata.Clone();
                
                if (originalComponent != null) {
                    var path = AssetDatabase.GetAssetPath(originalComponent);
                    componentData.asset = path;
                }
                
                artifactData.Modify();
            }

            // Force the hash to be the same as the original component
            if (originalComponent) componentToReconcile.hash = originalComponent.hash;
        }
    }
}
#endif