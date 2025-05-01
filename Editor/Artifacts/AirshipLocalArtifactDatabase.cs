#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Serialization;

namespace Airship.Editor {
    [Serializable]
    internal class ComponentData {
        
        /// <summary>
        /// The script path of this component
        /// </summary>
        public string script;
        /// <summary>
        /// The asset path of this component (if it's in a prefab)
        /// </summary>
        public string asset;
        /// <summary>
        /// The globally-unique identifier of this component
        /// </summary>
        public string guid;
        /// <summary>
        /// Metadata of this component
        /// </summary>
        public TypescriptCompilerMetadata metadata;
        
        private AirshipComponent _component;
        /// <summary>
        /// Will find the component via <tt>Resources.FindObjectsOfTypeAll&lt;T&gt;</tt>, and cache the result
        /// </summary>
        [CanBeNull] public AirshipComponent Component {
            get {
                if (_component) return _component;
                var components = Resources.FindObjectsOfTypeAll<AirshipComponent>();
                foreach (var component in components) {
                    if (component.guid != guid) continue; 
                    
                    _component = component;
                    break;
                }

                return _component;
            }
        }

        [CanBeNull] public GameObject Prefab => asset != null ? AssetDatabase.LoadAssetAtPath<GameObject>(asset) : null;
        
        /// <summary>
        /// Will return if the given component is synchronized with this data
        /// </summary>
        /// <param name="component">The component</param>
        /// <returns>True if the component matches this data</returns>
        public bool IsSyncedWith(AirshipComponent component) {
            if (guid != component.guid) return false;
            if (metadata == null) return false;
            
            return component.componentHash == metadata.hash;
        }
    }
    
    [Serializable]
    internal class ComponentScriptAssetData {
        public string script;
        public TypescriptCompilerMetadata metadata;
        
        /// <summary>
        /// Will find the script via <tt>AssetDatabase.LoadAssetAtPath&lt;T&gt;(assetPath)</tt>
        /// </summary>
        public AirshipScript Script => AssetDatabase.LoadAssetAtPath<AirshipScript>(script);
        
        /// <summary>
        /// Will return whether or not the given component data is newer than the script data
        /// </summary>
        public bool IsNewerThan(ComponentData componentData) {
            if (componentData.metadata == null) return true;
            var componentMetadata = componentData.metadata;
            
            // If the component is newer, than yes the script is outdated
            return componentMetadata < metadata;
        }

        /// <summary>
        /// Returns whether this component script data hash matches the given component script data hash
        /// </summary>
        public bool HasSameHashAs(ComponentData componentData) {
            if (componentData.metadata == null) return false;
            return componentData.metadata.hash == metadata.hash;
        }


        /// <summary>
        /// Will return true if the hash of the component isn't the same as the DB stored script hash
        /// </summary>
        public bool IsNotSameHashAsComponent(AirshipComponent component) {
            return component.componentHash != metadata.hash;
        }
    }

    /// <summary>
    /// The local artifact database for Airship's Editor - stored in <code>Library/AirshipArtifactDB</code>
    /// This contains the state of the scripts and components for the local project
    /// </summary>
    [FilePath("Library/AirshipArtifactDB", FilePathAttribute.Location.ProjectFolder)]
    internal class AirshipLocalArtifactDatabase : ScriptableSingleton<AirshipLocalArtifactDatabase> {
        [SerializeField] internal ReconcilerVersion reconcilerVersion = ReconcilerVersion.Default;
        [SerializeField] internal List<ComponentScriptAssetData> scripts = new();
        [SerializeField] internal List<ComponentData> components = new();
        
        /// <summary>
        /// Returns if the Database is empty - if it is, it's likely the project is new or freshly pulled
        /// </summary>
        internal static bool isEmpty => instance.scripts.Count == 0 && instance.components.Count == 0;
        
        /// <summary>
        /// Gets or creates the script asset data in the artifact database for the given script
        /// </summary>
        internal ComponentScriptAssetData GetOrCreateScriptAssetData(AirshipScript script) {
            var item = scripts.FirstOrDefault(f => f.script == script.assetPath);
            if (item == null) {
                item = new ComponentScriptAssetData() {
                    script = script.assetPath,
                    metadata = null,
                };
                scripts.Add(item);
            }
            return item;
        }

        /// <summary>
        /// Will try to get the component data associated with the specified component (if applicable)
        /// </summary>
        internal bool TryGetComponentData(AirshipComponent component, out ComponentData componentData, string assetPath = null) {
            var item = components.FirstOrDefault(f => f.guid == component.guid);
            if (item != null) {
                componentData = item;
                return true;
            }

            componentData = null;
            return false;   
        }
        
        /// <summary>
        /// Will try to get the script asset data associated with the specified script (if applicable)
        /// </summary>
        internal bool TryGetScriptAssetData(AirshipScript script, out ComponentScriptAssetData assetData) {
            var item = scripts.FirstOrDefault(f => f.script == script.assetPath);
            if (item != null) {
                assetData = item;
                return true;
            }
            assetData = null;
            return false;
        }

        /// <summary>
        /// Remove the component data for the given component - usually called when the component is destroyed or
        /// no longer wants to be referenced in the database
        /// </summary>
        internal bool RemoveComponentData(AirshipComponent component) {
            var item = components.FirstOrDefault(f => f.guid == component.guid);
            if (item == null) return false;
            components.Remove(item);
            return true;
        }

        private void OnEnable() {
#if AIRSHIP_DEBUG
            Debug.Log($"[LocalArtifactDB] Artifact DB enabled");
#endif
            
            // Dictionary<string, AirshipScript> pathToScript = new();
            // foreach (var script in Resources.FindObjectsOfTypeAll<AirshipScript>()) {
            //     if (pathToScript.ContainsKey(script.assetPath)) continue; // skip duplicates
            //     pathToScript.Add(script.assetPath, script);
            // }
            
            // When enabled we kind of want to run a validation of the component list
            Dictionary<string, AirshipComponent> guidToComponent = new();
            foreach (var component in Resources.FindObjectsOfTypeAll<AirshipComponent>()) {
                if (component != null && guidToComponent.ContainsKey(component.guid)) continue; // skip duplicates
                guidToComponent.Add(component.guid, component);
            }

            // Check each entry, if it has an active matching guid in memory we wanna skip, otherwise
            // we'll yeet it from existence - we don't care, and unity isn't going to try to reconcile it.
            foreach (var componentData in components.ToArray()) {
                if (guidToComponent.TryGetValue(componentData.guid, out _)) continue;
                components.Remove(componentData); // ??
#if AIRSHIP_DEBUG
                Debug.Log($"[LocalArtifactDB] Clean out component guid {componentData.guid}");
#endif
            }
            
//             foreach (var scriptAssetData in scripts.ToArray()) { /// ???
//                 if (pathToScript.TryGetValue(scriptAssetData.assetPath, out _)) continue;
//                 scripts.Remove(scriptAssetData);
// #if AIRSHIP_DEBUG
//                 Debug.Log($"[LocalArtifactDB] Clean out script {scriptAssetData.assetPath}");
// #endif
//             }
        }

        internal void Rebuild() {
            this.components.Clear();
            this.scripts.Clear();
        }

        private void OnDisable() {
#if AIRSHIP_DEBUG
            Debug.Log($"[LocalArtifactDB] Artifact DB disabled");
#endif
        }

        /// <summary>
        /// Modifies any changes to the artifact database
        /// </summary>
        internal void Modify() {
            Save(true);
        }
    }
}
#endif