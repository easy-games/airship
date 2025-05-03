#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Editor.EditorInternal;
using Luau;
using Mirror.SimpleWeb;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal enum ReconcileStatus {
        Unsuccessful,
        Unchanged,
        Reconciled,
        ReconcileWasQueued,
    }
    
    /// <summary>
    /// Handles the reconciliation of AirshipComponents
    /// </summary>
    internal static class AirshipReconciliationService {
        public static ReconcilerVersion DefaultReconcilerVersion => ReconcilerVersion.Version2;
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
            if (component.script == null) {
                Debug.LogWarning("no script");
                return false;
            }
            if (component.script.m_metadata == null) {
                Debug.LogWarning("no metadata");
                return false;
            }
            
            
            var scriptMetadata = component.script.m_metadata;
            var componentMetadata = component.metadata;

            if (scriptMetadata == null) return false;
            componentMetadata.name = scriptMetadata.name;
            
            // Add missing properties
            foreach (var scriptProperty in scriptMetadata.properties) {
                var componentProperty = componentMetadata.FindProperty(scriptProperty.name);
                if (componentProperty == null) {
                    var element = scriptProperty.Clone();
                    componentMetadata.properties.Add(element);
                    componentProperty = element;
#if AIRSHIP_DEBUG
                    additions.Add(element.name); // ??
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
                    Debug.Log($"\t<color=#78f798>+ {ObjectNames.NicifyVariableName(addition)}</color>");
                }
	        
                foreach (var modification in modifications) {
                    Debug.Log($"\t<color=#f7f778>~ {ObjectNames.NicifyVariableName(modification)}</color>");
                }
	        
                foreach (var deletion in deletions) {
                    Debug.Log($"\t<color=#f77878>- {ObjectNames.NicifyVariableName(deletion)}</color>");
                }
            }
#endif
            // component.componentHash = component.script.sourceFileHash;
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

            if (script == null) return false;
            
            if (!reconcileList.TryGetValue(script.assetPath, out var componentSet)) return false;
            foreach (var component in componentSet) {
                if (!component) continue;
                component.ReconcileMetadata(ReconcileSource.ForceReconcile, script.m_metadata);
            }
            
            reconcileList.Remove(script.assetPath);
            return true;
        }

#if AIRSHIP_DEBUG
        [MenuItem("Airship/Clear Artifact DB")]
        internal static void Clear() {
            AirshipLocalArtifactDatabase.instance.components.Clear();
            AirshipLocalArtifactDatabase.instance.scripts.Clear();
            AirshipLocalArtifactDatabase.instance.Modify();
        }
        
        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Reconcile Component", priority = 0)]
        internal static void Reconcile(MenuCommand item) {
            var component = item.context as AirshipComponent;
            ReconcileComponentUsingArtifacts(component, out _);
        }

        [MenuItem("CONTEXT/" + nameof(AirshipComponent) + "/Reconcile Component", validate = true)]
        internal static bool ValidateReconcile(MenuCommand command) {
            return ReconcilerVersion == ReconcilerVersion.Version2;
        }
#endif
        
        /// <summary>
        /// Reconciles the given component using the Artifact Database reconciliation system...
        /// </summary>
        /// <param name="component">The component to reconcile</param>
        /// <param name="status">The status of the reconcile</param>
        /// <returns>True if a reconcile is possible</returns>
        internal static bool ReconcileComponentUsingArtifacts(AirshipComponent component, out ReconcileStatus status) {
            if (component.script == null || component.script.m_metadata == null) {
#if AIRSHIP_DEBUG
                Debug.LogWarning($"[Reconcile] script or metadata missing for {component}", component);
#endif
                status = ReconcileStatus.Unsuccessful;
                return false;
            }
            
            var artifactData = AirshipLocalArtifactDatabase.instance;
            
            // Ensure we have the script asset data first, if not we'll just have to queue it for the compiler to process...
            var hasData = artifactData.TryGetScriptAssetData(component.script, out var scriptData);
            if (!hasData) {
#if AIRSHIP_DEBUG
                Debug.Log($"[Reconcile] Queued reconcile for {component.guid} (from {component.script.assetPath})");
#endif
                OnComponentQueueReconcile(component);
                status = ReconcileStatus.ReconcileWasQueued;
                return true;
            }
            
            // If the component artifact doesn't exist yet, create it.
            var components = artifactData.components;
            var componentData = components.FirstOrDefault(f => component.guid == f.guid);
            if (componentData == null) {
                componentData = new ComponentData() {
                    guid = component.guid,
                    script = component.script.assetPath,
                    metadata = scriptData.metadata.Clone(),
                };
                
                components.Add(componentData);
                artifactData.Modify();
            }
            
            // // Hash mismatch
            // if (!string.IsNullOrEmpty(component.componentHash) && scriptData.HasSameHashAs(componentData) && scriptData.IsNotSameHashAsComponent(component)) {
            //     Debug.Log($"[Reconcile] Queued reconcile fpr {component.componentHash}");
            //     OnComponentQueueReconcile(component);
            //     status = ReconcileStatus.ReconcileWasQueued;
            //     return true;
            // }

            var reconciled = ReconcileComponent(component);
            // Version mismatch
            if (scriptData.IsNewerThan(componentData) || scriptData.HasSameHashAs(componentData)) {
                if (reconciled) {
#if AIRSHIP_DEBUG
                    Debug.Log($"[Reconcile] Reconciled component because script newer tha component: {component.guid} (from {component.script.assetPath})");
#endif
                    componentData.metadata = scriptData.metadata.Clone();
                    status = ReconcileStatus.Reconciled;
                    return true;
                }
                else {
#if AIRSHIP_DEBUG
                    Debug.LogWarning($"[Reconcile] Failed reconcile from: {component.guid} (from {component.script.assetPath})");
#endif
                    status = ReconcileStatus.Unsuccessful;
                    return false;
                }
            }
            
            status = ReconcileStatus.Reconciled;
            return true;
        }

        private static IEnumerator OnDeferComponentReconcile(AirshipReconcileEventData eventData) {
            yield return new WaitForEndOfFrame();
            OnComponentReconcile(eventData);
        }
        
        /// <summary>
        /// Callback for when a component is requesting reconciliation
        /// </summary>
        private static void OnComponentReconcile(AirshipReconcileEventData eventData) {
            // Components must have guids
            if (string.IsNullOrEmpty(eventData.Component.guid)) eventData.Component.guid = Guid.NewGuid().ToString();

            // If an initial setup (e.g. first pull, or new template project)
            if (AirshipLocalArtifactDatabase.isEmpty) {
                // We can run a default reconcile, it wont matter tbh.
                var component = eventData.Component;
                ReconcileComponent(component);
                //component.componentHash = component.scriptHash;
                return;
            }
            
            if (ReconcilerVersion == ReconcilerVersion.Version2) {
                var component = eventData.Component;

                var isPrefab = PrefabUtility.IsPartOfAnyPrefab(component);
                var prefabOriginalComponent = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                if (isPrefab && prefabOriginalComponent.script != null) {
                    // If it's a instance component:
                    // - We need to reconcile the source component first:
                    //      - If successful: We can then reconcile the instance
                    //      - Else: well, no point doing a reconcile here... it would just be desync
                    
                    if (prefabOriginalComponent.script == null) {
                        Debug.LogWarning("[Reconcile] Attempted reconcile on instance prefab original without script attached?");
                        return;
                    }
                    
                    if (ReconcileComponentUsingArtifacts(prefabOriginalComponent, out var result)) {
                        // if successful, we can determine whether or not we'll reconcile the instance component
                        
                        switch (result) {
                            // Once we're aware the original is updated, reconcile the instance copy!
                            case ReconcileStatus.Reconciled: {
                                ReconcileComponent(component);
                                break;
                            }
                            case ReconcileStatus.ReconcileWasQueued:
                            case ReconcileStatus.Unchanged:
                                break;
                            default:
                                Debug.Log($"Failed to reconcile {component.guid}, got {result}");
                                break;
                        }
                    }
                }
                else {
                    // It's just an orphaned instance, or we're reconciling the original component, we can just reconcile it outright. No silly business required.
                    ReconcileComponentUsingArtifacts(component, out _);
                }
            }
            else {
                var component = eventData.Component;
                ReconcileComponent(component);
                //component.componentHash = component.scriptHash;
            }
        }
    }
}
#endif