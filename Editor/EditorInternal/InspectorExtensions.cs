#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;

public static class InspectorExtensions {
    internal static UnityEditor.Editor GetFirstNonImportInspectorEditor(UnityEditor.Editor[] editor) {
        return InspectorWindowUtils.GetFirstNonImportInspectorEditor(editor);
    }

    internal static IEnumerable<UnityEditor.Editor> GetEditorsFromWindow(EditorWindow window) {
        if (window is PropertyEditor propertyEditorWindow) {
            return propertyEditorWindow.tracker.activeEditors;
        }

        return new UnityEditor.Editor[] { };
    }
}
#endif