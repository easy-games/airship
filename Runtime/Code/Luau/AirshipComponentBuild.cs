using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Luau {
    /// <summary>
    /// Temporary intermediate class used for deserializing the JSON data.
    /// </summary>
    public class AirshipBehaviourMetaTop {
        // ReSharper disable once CollectionNeverUpdated.Global
        // ReSharper disable once UnassignedField.Global
        public Dictionary<string, AirshipBehaviourMeta> behaviours;

        private AirshipBehaviourMetaTop() { }
    }
    
    /// <summary>
    /// Defines each AirshipBehaviour component class.
    /// </summary>
    [Serializable]
    public class AirshipBehaviourMeta {
        public string className;
        public bool component;
        public string filePath;
        public List<string> extends;

        private AirshipBehaviourMeta() {}
    }
    
    [Serializable]
    public class AirshipBuildData {
        public List<AirshipBehaviourMeta> airshipBehaviourMetas;
        
        /// <summary>
        /// Build AirshipBuildData from JSON. Used by the AirshipComponentBuildImporter.
        /// </summary>
        public static AirshipBuildData FromJsonData(string data) {
            var meta = JsonConvert.DeserializeObject<AirshipBehaviourMetaTop>(data);
            var buildData = new AirshipBuildData(meta);
            return buildData;
        }

        private AirshipBuildData(AirshipBehaviourMetaTop metaTop) {
            airshipBehaviourMetas = new List<AirshipBehaviourMeta>(metaTop.behaviours.Count);
            foreach (var pair in metaTop.behaviours) {
                pair.Value.className = pair.Key;
                pair.Value.filePath = pair.Value.filePath.Replace("\\", "/");
                airshipBehaviourMetas.Add(pair.Value);
            }
        }
    }
    
    public class AirshipComponentBuild : ScriptableObject {
        public AirshipBuildData data;

        private readonly Dictionary<string, AirshipBehaviourMeta> _classes = new();
        private bool _init = false;

        private void Init() {
            _init = true;
            foreach (var meta in data.airshipBehaviourMetas) {
                _classes.Add(meta.className, meta);
            }
        }

        public bool Has(string airshipBehaviourClassName) {
            if (!_init) {
                Init();
            }
            
            return _classes.ContainsKey(airshipBehaviourClassName);
        }

        public string GetScriptPath(string airshipBehaviourClassName) {
            if (!_init) {
                Init();
            }

            var meta = _classes[airshipBehaviourClassName];
            return meta.filePath;
        }
    }
}
