using UnityEditor;

namespace Editor.EditorInternal {
    public static class LogExtensions {
        public static int GetLogCount() {
            return LogEntries.GetCount();
        }
    }
}