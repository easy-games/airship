using JetBrains.Annotations;
using UnityEngine;

namespace Luau { 
    [System.Serializable]
    public class BinaryFile : ScriptableObject {
        [HideInInspector]
        public string m_path;
        public byte[] m_bytes;
        public bool m_compiled = false;
        public bool m_forceNativeCodeGen;
        [TextArea(15, 20)]
        public string m_compilationError = "";
        [CanBeNull] public LuauMetadata m_metadata;
        public bool airshipBehaviour;
    }
}
