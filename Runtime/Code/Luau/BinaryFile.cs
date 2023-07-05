
using System;
using UnityEngine;
using UnityEditor;

namespace Luau
{ 
    [System.Serializable]
    public class BinaryFile : ScriptableObject
    {
        
        public byte[] m_bytes;
        public bool m_compiled = false;
        [TextArea(15,20)]
        public string m_compilationError = "";
    }
}
