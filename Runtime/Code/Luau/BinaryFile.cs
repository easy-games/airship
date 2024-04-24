using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Luau { 
    [System.Serializable]
    public class BinaryFile : ScriptableObject {
        [HideInInspector]
        public string m_path;
        public byte[] m_bytes;
        public bool m_compiled = false;
        [TextArea(15, 20)]
        public string m_compilationError = "";
        [CanBeNull] public LuauMetadata m_metadata;
        public bool airshipBehaviour;

        public static BinaryFile GetBinaryFileFromPath(string path) {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<BinaryFile>(path);
#else
            return null;
#endif
        }
    }
}
