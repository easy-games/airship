using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Luau {
    public enum AirshipScriptLanguage {
        Typescript,
        Luau,
    }

    /// <summary>
    /// An object that compiles to Luau to be executed by the Luau runtime
    /// </summary>
    public abstract class AirshipScriptable : ScriptableObject {
        /// <summary>
        /// This is the path of the asset itself - used for the editor
        /// </summary>
        public string assetPath;
        
        public AirshipScriptLanguage scriptLanguage;
        
        // [HideInInspector]
        public string m_path;
        
        #region Luau Properties
        public byte[] m_bytes;
        
        public bool m_compiled = false;
        [TextArea(15, 20)]
        public string m_compilationError = "";
        #endregion

        [CanBeNull] public LuauMetadata m_metadata;
    }
    
    [Serializable]
    public class AirshipScript : AirshipScriptable {
        #region Typescript Properties
        [FormerlySerializedAs("tsWasCompiled")] public bool typescriptWasCompiled = false;
        #endregion

        public bool airshipBehaviour;
        
        public static AirshipScript GetBinaryFileFromPath(string path) {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<AirshipScript>(path);
#else
            return null;
#endif
        }
    }
}
