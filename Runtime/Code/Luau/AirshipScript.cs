using System;
using System.IO;
using System.Security.Cryptography;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Luau {
    public enum AirshipScriptLanguage {
        Typescript,
        Luau,
    }
    
    [Serializable]
    internal class TypescriptCompilerMetadata {
        /// <summary>
        /// The file hash at the compile time
        /// </summary>
        public string hash;
        /// <summary>
        /// The timestamp when this was compiled
        /// </summary>
        public long timestamp;
        public bool valid => hash != default && timestamp != default;
        public DateTimeOffset dateTimeOffset => DateTimeOffset.FromUnixTimeSeconds(timestamp);
        public DateTime dateTime => dateTimeOffset.UtcDateTime;

        public TypescriptCompilerMetadata Clone() {
            return new TypescriptCompilerMetadata() {
                hash = hash,
                timestamp = timestamp,
            };
        }
        
        /// <summary>
        /// Is the left metadata older than the right metadata
        /// </summary>
        public static bool operator<(TypescriptCompilerMetadata lhs, TypescriptCompilerMetadata rhs) {
            return lhs.timestamp < rhs.timestamp;
        }
        
        /// <summary>
        /// Is the left metadata newer than the right metadata
        /// </summary>
        public static bool operator>(TypescriptCompilerMetadata lhs, TypescriptCompilerMetadata rhs) {
            return lhs.timestamp > rhs.timestamp;
        }

        public override string ToString() {
            var formatted = dateTime.ToString("yy-MM-dd h:mm:ss tt zz");
            return $"{hash} ({formatted})";
        }
    }
    
    [Serializable]
    public class AirshipScript : ScriptableObject {
        // [HideInInspector]
        public string m_path;
        
        /// <summary>
        /// This is the path of the asset itself - used for the editor
        /// </summary>
        public string assetPath;
        public string compiledFileHash;
        
        public AirshipScriptLanguage scriptLanguage;
        
        #region Typescript Properties
        [FormerlySerializedAs("tsWasCompiled")] public bool typescriptWasCompiled = false;
        
        #endregion
        
        #region Luau Properties
        [HideInInspector]
        public byte[] m_bytes;
        
        public bool m_compiled = false;
        [TextArea(15, 20)]
        public string m_compilationError = "";

        public string[] m_directives;
        public string[] m_directiveValues;
        #endregion
        
        [CanBeNull] public LuauMetadata m_metadata;
        public bool airshipBehaviour;
        
        /// <summary>
        /// Used to check if the file has changed but not been recompiled yet
        /// </summary>
        public bool HasFileChanged => compiledFileHash != sourceFileHash;
        
        public string sourceFileHash {
            get {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(assetPath);
                var hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return hash;
            }
        }

        public bool HasDirective(string directive) {
            if (m_directives != null) {
                foreach (var dir in m_directives) {
                    if (dir == directive) {
                        return true;
                    }
                }
            }

            return false;
        }

        public string GetDirectiveValue(string directive) {
            if (m_directives != null && m_directiveValues != null && m_directives.Length == m_directiveValues.Length) {
                for (var i = 0; i < m_directives.Length; i++) {
                    if (m_directives[i] == directive) {
                        return m_directiveValues[i];
                    }
                }
            }

            return null;
        }
        
        public static AirshipScript GetBinaryFileFromPath(string path) {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<AirshipScript>(path);
#else
            return null;
#endif
        }
    }
}
