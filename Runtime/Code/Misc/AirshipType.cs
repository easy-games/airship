using System;
using System.Linq;
using JetBrains.Annotations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luau {
    public enum AirshipDeclarationType {
        Unknown,
        /// <summary>
        /// An AirshipBehaviour class
        /// </summary>
        AirshipBehaviour,
        /// <summary>
        /// An enum
        /// </summary>
        Enum,
        /// <summary>
        /// An AirshipScriptableObject class
        /// </summary>
        [Obsolete] // not yet impl
        AirshipScriptableObject,
        /// <summary>
        /// A class that is @Serializable()
        /// </summary>
        [Obsolete] // not yet impl
        SerializableClass,
    }
    
    /// <summary>
    /// Represents an Airship Type
    /// </summary>
    public class AirshipType {
        [CanBeNull]
        public static AirshipType GetType(string typeName) => AirshipBuildInfo.Instance.GetTypeByName(typeName);
        
        /// <summary>
        /// The name of this type
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The file path of this type at runtime (will be a relative path Lua file)
        /// </summary>
        public string RuntimePath { get; }
        /// <summary>
        /// The TypeScript file path of this type (will be a TypeScript file in Assets)
        /// </summary>
        public string AssetPath => "Assets/" + RuntimePath.Replace(".lua", ".ts");
        /// <summary>
        /// The types this Type inherits
        /// </summary>
        public AirshipType[] BaseTypes { get; internal set; }
        /// <summary>
        /// A unique identifier for this type
        /// </summary>
        public string UniqueId => $"{RuntimePath.Replace(".lua", "")}@{Name}";
        /// <summary>
        /// What this type is declared as
        /// </summary>
        public AirshipDeclarationType DeclarationType { get; }

        public bool IsAssignableFrom(AirshipType baseType) {
            return baseType == this ||  BaseTypes.Contains(baseType);
        }

        public bool IsAncestorOfType(AirshipType other) {
            return other.BaseTypes.Contains(this);
        }
        
        public AirshipScript Script {
            get {
#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<AirshipScript>(AssetPath);
#else
                return null;
#endif
            }
        }

        public AirshipType(AirshipBehaviourMeta meta) {
            Name = meta.className;
            DeclarationType = meta.component ? AirshipDeclarationType.AirshipBehaviour : AirshipDeclarationType.Unknown;
            RuntimePath = meta.filePath;
        }
        
        public AirshipType(TypeScriptEnum @enum) {
            var name = @enum.id.Split("@").Last();
            var path = string.Join("@", @enum.id.Split("@")[0..^2]);
            
            Name = name;
            DeclarationType = AirshipDeclarationType.Enum;
            RuntimePath = path + ".lua";
        }
        
        public static implicit operator AirshipType(string typeName) {
            return GetType(typeName);
        }

        public override int GetHashCode() {
            return UniqueId.GetHashCode();
        }
        
        protected bool Equals(AirshipType other) {
            return UniqueId == other.UniqueId;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AirshipType)obj);
        }
        
        public static bool operator ==(AirshipType left, AirshipType right) {
            var boxedLeft = (object)left;
            var boxedRight = (object)right;
            
            if (boxedLeft == null && boxedRight == null) return true;
            if (boxedLeft == null) return false;
            if (boxedRight == null) return false;
         
            
            return left!.UniqueId == right!.UniqueId;
        }
        
        public static bool operator !=(AirshipType left, AirshipType right) {
            return !(left == right);
        }
    }
}