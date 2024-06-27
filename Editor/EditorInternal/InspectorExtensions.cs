#if UNITY_EDITOR

using UnityEditor;
namespace Editor.EditorInternal {
    public static class InspectorExtensions {
        public static UnityEditor.Editor GetFirstNonImportInspectorEditor(UnityEditor.Editor[] editor) {
            return InspectorWindowUtils.GetFirstNonImportInspectorEditor(editor);
        }
    }
}

#endif