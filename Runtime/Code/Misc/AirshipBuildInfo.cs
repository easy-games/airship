using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    
    public class AirshipBuildInfo : ScriptableObject {
        private const string BundlePath = "Airship.asbuildinfo";
        
        private static AirshipBuildInfo _instance = null;
        
        public AirshipBuildData data;

        private readonly Dictionary<string, AirshipBehaviourMeta> _classes = new();

        public static AirshipBuildInfo Instance {
            get {
                if (_instance != null) {
                    return _instance;
                }
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying) {
                    _instance = AssetDatabase.LoadAssetAtPath<AirshipBuildInfo>($"Assets/{BundlePath}");
                }
#endif
                if (_instance == null && AssetBridge.Instance != null && AssetBridge.Instance.IsLoaded()) {
                    _instance = AssetBridge.Instance.LoadAssetInternal<AirshipBuildInfo>(BundlePath);
                }

                if (_instance != null) {
                    _instance.Init();
                } else {
                    Debug.LogWarning("Failed to load AirshipBuildInfo");
                }

                return _instance;
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _instance = null;
        }

        private void Init() {
            foreach (var meta in data.airshipBehaviourMetas) {
                _classes.TryAdd(meta.className, meta);
            }
        }

        public bool HasAirshipBehaviourClass(string airshipBehaviourClassName) {
            return _classes.ContainsKey(airshipBehaviourClassName);
        }

        public string GetScriptPath(string airshipBehaviourClassName) {
            var meta = _classes[airshipBehaviourClassName];
            return meta.filePath;
        }
    }
}
