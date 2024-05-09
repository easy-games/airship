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
    
    [Serializable]
    public class BinaryFile : ScriptableObject {
        [Obsolete("This is going away in the future, do not rely on this anymore")]
        [HideInInspector]
        public string m_path;
        
        /// <summary>
        /// This is the path of the asset itself - used for the editor
        /// </summary>
        public string assetPath;
        
        public AirshipScriptLanguage scriptLanguage;
        
        #region Typescript Properties
        #endregion
        
        #region Luau Properties
        public byte[] m_bytes;
        
        public bool m_compiled = false;
        [TextArea(15, 20)]
        public string m_compilationError = "";
        #endregion
        
        [CanBeNull] public LuauMetadata m_metadata;
        public bool airshipBehaviour;
    }
}
