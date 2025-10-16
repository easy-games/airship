using JetBrains.Annotations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luau {
    public class AirshipType {
        [CanBeNull]
        public static AirshipType GetType(string typeName) => AirshipBuildInfo.Instance.GetTypeByName(typeName);
        
        public string Name { get; }
        public string RuntimePath { get; }
        public string AssetPath => "Assets/" + RuntimePath.Replace(".lua", ".ts");
        public AirshipType[] BaseTypes { get; internal set; }
        public string UniqueId => $"{RuntimePath.Replace(".lua", "")}@{Name}";
        public bool AirshipBehaviour { get; }
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
            AirshipBehaviour = meta.component;
            RuntimePath = meta.filePath;
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