using UnityEditor;

namespace Editor.EditorInternal {
    public class AirshipEditorInternals {
        internal static bool SetBoldDefaultFont(bool isBold) {
            var wasBold = EditorGUIUtility.GetBoldDefaultFont();
            EditorGUIUtility.SetBoldDefaultFont(isBold);
            return wasBold;
        }

        internal static bool GetBoldDefaultFont() {
            return EditorGUIUtility.GetBoldDefaultFont();
        }
    }
}