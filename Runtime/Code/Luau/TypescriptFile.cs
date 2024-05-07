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
        [CanBeNull] public BinaryFile binaryFile;
    }
}