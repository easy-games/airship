#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Airship.Editor {
    [Serializable]
    internal class ComponentData {
        public string scriptPath;
        public string guid;
        public TypescriptCompilerMetadata metadata;

        // public bool IsOlderThan(ScriptAssetData scriptAssetData) {
        //     if (scriptAssetData == null) return false;
        //     return scriptAssetData.metadata > metadata;
        // }
    }
    
    [Serializable]
    internal class ScriptAssetData {
        public string assetPath;
        public TypescriptCompilerMetadata metadata;
        public AirshipScript script => AssetDatabase.LoadAssetAtPath<AirshipScript>(assetPath);
        public bool IsOlderThan(ComponentData componentData) {
            if (componentData.metadata == null) return true;
            var componentMetadata = componentData.metadata;
            
            // If the component is newer, than yes the script is outdated
            return componentMetadata > metadata;
        }
        
        public bool IsNewerThan(ComponentData componentData) {
            if (componentData.metadata == null) return true;
            var componentMetadata = componentData.metadata;
            
            // If the component is newer, than yes the script is outdated
            return componentMetadata < metadata;
        }

        public bool IsOutOfSyncWith(ComponentData componentData) {
            if (componentData.metadata == null) return false;
            var componentMetadata = componentData.metadata;
            
            // If our behaviour is supposedly "older" than the script, BUT
            // the behaviour is also different... we can assume desync? 
            return !IsOlderThan(componentData) && componentMetadata.hash != script.compiledFileHash;
        }
    }

    [FilePath("Library/AirshipArtifactDB", FilePathAttribute.Location.ProjectFolder)]
    internal class AirshipLocalArtifactDatabase : ScriptableSingleton<AirshipLocalArtifactDatabase> {
        [SerializeField] internal List<ScriptAssetData> scripts = new();
        [SerializeField] internal List<ComponentData> components = new();
        
        public ScriptAssetData GetOrCreateScriptAssetData(AirshipScript script) {
            var item = scripts.FirstOrDefault(f => f.assetPath == script.assetPath);
            if (item == null) {
                item = new ScriptAssetData() {
                    assetPath = script.assetPath,
                    metadata = null,
                };
                scripts.Add(item);
            }
            return item;
        }

        public bool TryGetComponentData(AirshipComponent component, out ComponentData componentData) {
            var item = components.FirstOrDefault(f => f.guid == component.guid);
            if (item != null) {
                componentData = item;
                return true;
            }

            componentData = null;
            return false;   
        }
        
        public bool TryGetScriptAssetData(AirshipScript script, out ScriptAssetData assetData) {
            var item = scripts.FirstOrDefault(f => f.assetPath == script.assetPath);
            if (item != null) {
                assetData = item;
                return true;
            }

            assetData = null;
            return false;
        }

        public bool RemoveComponentData(AirshipComponent component) {
            var item = components.FirstOrDefault(f => f.guid == component.guid);
            if (item == null) return false;
            components.Remove(item);
            return true;
        }

        private void OnEnable() {
#if AIRSHIP_DEBUG
            Debug.Log($"[LocalArtifactDB] Artifact DB enabled");
#endif
            
            // When enabled we kind of want to run a validation of the component list
            Dictionary<string, AirshipComponent> guidToComponent = new();
            foreach (var component in Resources.FindObjectsOfTypeAll<AirshipComponent>()) {
                if (guidToComponent.ContainsKey(component.guid)) continue; // skip duplicates
                guidToComponent.Add(component.guid, component);
            }

            // Check each entry, if it has an active matching guid in memory we wanna skip, otherwise
            // we'll yeet it from existence - we don't care, and unity isn't going to try to reconcile it.
            foreach (var componentData in components.ToArray()) {
                if (guidToComponent.TryGetValue(componentData.guid, out _)) continue;
                components.Remove(componentData);
#if AIRSHIP_DEBUG
                Debug.Log($"[LocalArtifactDB] Clean out component guid {componentData.guid}");
#endif
            }
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