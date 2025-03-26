#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
    
    [Serializable]
    internal class PrefabAssetData {
        public string assetPath;
        public List<ComponentData> components = new();

        public ComponentData GetOrCreateComponentData(AirshipComponent component) {
            // component.GetInstanceID()
            
            return null;
        }
    }
    
    [Serializable]
    internal class ScriptAssetData {
        public string assetPath;
        public TypescriptCompilerMetadata metadata;
    }
    
    [FilePath("Library/AirshipArtifactDB", FilePathAttribute.Location.ProjectFolder)]
    internal class AirshipLocalArtifactDatabase : ScriptableSingleton<AirshipLocalArtifactDatabase> {
        [FormerlySerializedAs("scriptAssets")] [SerializeField]
        private List<ScriptAssetData> scripts = new();
        [SerializeField] private List<ComponentData> components = new();

        [InitializeOnLoadMethod]
        public static void OnLoad() {
            AirshipComponent.Reconcile += OnComponentReconcile;
        }

        private static void OnComponentReconcile(AirshipReconcileEventData eventData) {
            var component = eventData.Component;
            
            if (!component.script) return;
            
            var components = instance.components;
            var componentData = components.FirstOrDefault(f => component.guid == f.guid);
            if (componentData == null) {
                componentData = new ComponentData() {
                    guid = component.guid,
                    scriptPath = component.scriptPath,
                };
                components.Add(componentData);
            }

            var scriptData = instance.GetScriptAssetData(component.script);
            if (componentData.metadata == null || componentData.metadata.timestamp < scriptData.metadata.timestamp) {
                componentData.metadata = scriptData.metadata.Clone();
                instance.Modify();
            }
            else {
                eventData.ShouldReconcile = false;
            }
        }

#if AIRSHIP_DEBUG
        [MenuItem("Airship/Wipe Editor Data")]
        public static void Wipe() {
            instance.scripts = new List<ScriptAssetData>();
            instance.components = new List<ComponentData>();
            instance.Modify();
        }
#endif
        
        public ScriptAssetData GetScriptAssetData(AirshipScript script) {
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
        
        private void OnEnable() {
            
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