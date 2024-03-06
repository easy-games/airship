#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Code.State {
    [LuauAPI]
    public class EditorSessionState : Singleton<EditorSessionState> {
        public static void SetString(string key, string value) {
#if UNITY_EDITOR
            SessionState.SetString(key, value);
#endif
        }

        public static string GetString(string key) {
#if UNITY_EDITOR
            return SessionState.GetString(key, null);
#endif
            return null;
        }

        public static bool GetBoolean(string key) {
#if UNITY_EDITOR
            return SessionState.GetBool(key, false);
#endif
            return false;
        }

        public static void RemoveString(string key) {
#if UNITY_EDITOR
            SessionState.EraseString(key);
#endif
        }
    }
}