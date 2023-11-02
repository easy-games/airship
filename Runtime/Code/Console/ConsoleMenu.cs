using UnityEngine;

namespace Code.Console {
    public class ConsoleMenu : MonoBehaviour {
        public Canvas canvas;

        private void OnEnable() {
            Application.logMessageReceived += LogCallback;
        }

        private void OnDisable() {
            Application.logMessageReceived -= LogCallback;
        }

        public void LogCallback(string message, string stackTrace, LogType type) {
            string s = message;
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
                s += " " + stackTrace;
            }
        }
    }
}