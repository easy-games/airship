using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
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
        AirshipScriptableObject,
        /// <summary>
        /// A class that is @Serializable()
        /// </summary>
        SerializableClass,
    }
    
    /// <summary>
    /// Represents an Airship Type
    /// </summary>
    public sealed class AirshipType {
        /// <summary>
        /// Query an Airship type by name
        /// </summary>
        /// <param name="typeName">The name of the type to get</param>
        /// <returns>The type, or null if not found</returns>
        [CanBeNull]
        public static AirshipType GetType(string typeName) => AirshipBuildInfo.Instance.GetTypeByName(typeName);

        public bool IsAbstract { get; }
        public bool IsDefault { get; }

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

        internal List<AirshipType> childTypes = new();
        public AirshipType[] ChildTypes => childTypes.ToArray();
        /// <summary>
        /// A unique identifier for this type
        /// </summary>
        public string UniqueId => $"{RuntimePath.Replace(".lua", "")}@{Name}";
        /// <summary>
        /// What this type is declared as
        /// </summary>
        public AirshipDeclarationType DeclarationType { get; }

        /// <summary>
        /// Returns whether or not this type can be assigned from the given base type
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public bool IsAssignableFrom(AirshipType baseType) {
            return this == baseType || BaseTypes.Contains(baseType);
        }
        
        /// <summary>
        /// The script declaring this type
        /// </summary>
        public AirshipScript Script {
            get {
#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<AirshipScript>(AssetPath);
#else
                return null;
#endif
            }
        }

        internal AirshipType(AirshipTypeInfo typeMetadata) {
            Name = typeMetadata.name;
            DeclarationType = typeMetadata.declarationType;
            RuntimePath = Path.ChangeExtension(typeMetadata.file, ".lua");

            if (typeMetadata.modifiers != null) {
                IsAbstract = typeMetadata.modifiers.Contains("abstract");
                IsDefault = typeMetadata.modifiers.Contains("default");
            }
        }

        internal AirshipType(AirshipBehaviourMeta meta) {
            Name = meta.className;
            DeclarationType = meta.type switch {
                AirshipBehaviourMetaType.AirshipBehaviour => AirshipDeclarationType.AirshipBehaviour,
                AirshipBehaviourMetaType.AirshipScriptableObject => AirshipDeclarationType.AirshipScriptableObject,
                AirshipBehaviourMetaType.Serializable => AirshipDeclarationType.SerializableClass,
                _ => AirshipDeclarationType.Unknown
            };
            RuntimePath = meta.filePath;
        }
        
        internal AirshipType(TypeScriptEnum @enum) {
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
        
        private bool Equals(AirshipType other) {
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