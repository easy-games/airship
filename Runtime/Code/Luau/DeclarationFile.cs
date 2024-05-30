using UnityEngine;
using UnityEngine.Serialization;

namespace Luau {
    public class DeclarationFile : ScriptableObject {
        public bool ambient;
        public bool isLuauDeclaration;
        public string scriptPath;
    }
}