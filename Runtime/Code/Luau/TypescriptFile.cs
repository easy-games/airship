using System;
using JetBrains.Annotations;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace Luau {
    [System.Serializable]
    public class TypescriptFile : ScriptableObject {
        public string path;
        public bool compiled = false;
        
        
        /// <summary>
        /// The linked binary file assoc. with this TS file
        /// </summary>
        [CanBeNull] public BinaryFile binaryFile;
        
        [CanBeNull] public LuauMetadata Metadata => binaryFile ? binaryFile.m_metadata : null;
        [CanBeNull] public string LuauPath => binaryFile ? binaryFile.m_path : null;

        [CanBeNull] public bool IsAirshipComponent => binaryFile && binaryFile.airshipBehaviour;

        public static TypescriptFile[] Files {
            get {
#if UNITY_EDITOR
                var tsFileIds = AssetDatabase.FindAssets($"t:{nameof(TypescriptFile)}");
                
#endif
                return new TypescriptFile[] { };
            }
        }
        
        public void GetFiles() {
            
        }
    }
}