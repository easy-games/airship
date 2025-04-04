#if UNITY_EDITOR
using UnityEditor;

namespace Editor.EditorInternal {
    public static class AssetImporterExtensions {
        internal static bool IsPrefabImporter(this AssetImporter importer) {
            return importer is PrefabImporter;
        }

        internal static bool PrefabImportTest(this AssetImporter importer) {
            if (importer is not PrefabImporter prefabImporter) return false;
            
            return true;
        }
    }
}
#endif