#if UNITY_EDITOR
using UnityEditor;

namespace Easy.Airship.Editor.EditorInternal {
    public static class LogExtensions {
        internal static int GetLogCount() {
            return LogEntries.GetCount();
        }
    }
}
#endif