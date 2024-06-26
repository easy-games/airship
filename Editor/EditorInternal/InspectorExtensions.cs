using UnityEditor;

namespace Editor.EditorInternal {
    public class InspectorExtensions {
        public static UnityEditor.Editor GetFirstNonImportInspectorEditor(UnityEditor.Editor[] editor) {
            return InspectorWindowUtils.GetFirstNonImportInspectorEditor(editor);
        }
    }
}