using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public Dictionary<string, string[]> extends;

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
    public class AirshipExtendsMeta {
        public string id;
        public string[] extends;
        
        public string scriptPath;
        public string[] extendsScriptPaths;
    }
    
    [Serializable]
    public class AirshipBuildData {
        public List<AirshipBehaviourMeta> airshipBehaviourMetas;
        public List<AirshipExtendsMeta> airshipExtendsMetas;
        
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

            airshipExtendsMetas = new List<AirshipExtendsMeta>(metaTop.extends.Count);
            foreach (var pair in metaTop.extends) {
                var matching = metaTop.behaviours[pair.Key];
                if (matching == null) continue;
                
                var meta = new AirshipExtendsMeta();
                meta.scriptPath = matching.filePath.Replace("\\", "/");

                var extendsPaths = new List<string>();
                foreach (var extendsPath in pair.Value) {
                    var matchingExtends = metaTop.behaviours[extendsPath];
                    if (matchingExtends == null) continue;
                    extendsPaths.Add(matchingExtends.filePath);
                }

                meta.id = pair.Key;
                meta.extends = pair.Value;
                
                meta.extendsScriptPaths = extendsPaths.ToArray();
                airshipExtendsMetas.Add(meta);
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
                if (_instance == null) {
                    _instance = AssetDatabase.LoadAssetAtPath<AirshipBuildInfo>($"Assets/{BundlePath}");
                }
#endif
                if (SceneManager.GetActiveScene().name is "MainMenu") {
                    return null;
                }
                
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

        /// <summary>
        /// Checks a component inherits the given script
        /// </summary>
        /// <param name="component">The component to lookup</param>
        /// <param name="parentScript">The script to check against</param>
        /// <returns>True if component inherits script</returns>
        public bool ComponentIsValidInheritance(AirshipComponent component, AirshipScript parentScript) {
            return Inherits(component.scriptFile, parentScript);
        }

        private string StripAssetPrefix(string path) {
            return path.ToLower().StartsWith("assets/") ? path[7..] : path;
        }

        [CanBeNull]
        public string GetScriptPathByTypeName(string typeName) {
            return (from meta in data.airshipBehaviourMetas where meta.className == typeName select meta.filePath.Replace("\\", "/")).FirstOrDefault();
        }
        
        /// <summary>
        /// Checks if the child script at the childPath inherits the parent script at parentPath
        /// </summary>
        /// <param name="childPath">The path of the child script</param>
        /// <param name="parentPath">The path of the parent script</param>
        /// <returns>True if the child script inherits the parent script</returns>
        public bool Inherits(string childPath, string parentPath) {
            childPath = StripAssetPrefix(childPath).ToLower();
            parentPath = StripAssetPrefix(parentPath).ToLower();
            
            if (childPath == parentPath) return true;
            
            var extendsMeta = data.airshipExtendsMetas.Find(f => f.scriptPath.ToLower() == parentPath);
            if (extendsMeta == null) {
                return false;
            }
            
            var isExtending = extendsMeta.extendsScriptPaths.Select(path => path.ToLower()).Contains(childPath);
            return isExtending;
        }

        /// <summary>
        /// Checks if the child script inherits the script at the given parent path
        /// </summary>
        /// <param name="childScript">The child script</param>
        /// <param name="parentPath">The path of the parent script</param>
        /// <returns>True if the child script inherits the parent script</returns>
        public bool Inherits(AirshipScript childScript, string parentPath) {
            var childPath = childScript.m_path;
            return Inherits(childPath, parentPath);
        }
        
        /// <summary>
        /// Checks if the child script inherits the parent script
        /// </summary>
        /// <param name="childScript">The child script</param>
        /// <param name="parentScript">The parent script</param>
        /// <returns>True if the child script inherits the parent script</returns>
        public bool Inherits(AirshipScript childScript, AirshipScript parentScript) {
            var childPath = childScript.m_path;
            var parentPath = parentScript.m_path;

            return Inherits(childPath, parentPath);
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
