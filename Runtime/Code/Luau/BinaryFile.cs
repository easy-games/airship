using UnityEngine;

namespace Luau
{ 
    [System.Serializable]
    public class BinaryFile : ScriptableObject
    {
        // [HideInInspector]
        public byte[] m_bytes;
        public bool m_compiled = false;
        [TextArea(15,20)]
        public string m_compilationError = "";

        [HideInInspector]
        public LuauMetadata m_metadata;
    }
}
