#if UNITY_EDITOR
using UnityEditor;

namespace Editor.EditorInternal {
    public static class LogExtensions {
        internal static int GetLogCount() {
            return LogEntries.GetCount();
        }
    }
}
#endif