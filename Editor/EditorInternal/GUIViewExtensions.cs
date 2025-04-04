using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.EditorInternal {
    public static class GUIViewExtensions {
        public static float v = 10;
        internal static IMGUIContainer GetIMGUIContainerForStatusbar() {
#if !UNITY_EDITOR
            return null;
#else
            var sbar = Resources.FindObjectsOfTypeAll<AppStatusBar>().FirstOrDefault();
            if (sbar == null) {
                Debug.LogWarning("Could not find AppStatusBar");
                return null;
            }

            var backend = sbar.windowBackend;
            if (backend == null) {
                Debug.LogWarning("Could not find WindowBackend for AppStatusBar");
                return null;
            }

            var visualElement = ((VisualElement) backend.visualTree)[0];
            if (visualElement == null) {
                Debug.LogWarning("Could not find VisualElement for AppStatusBar");
                return null;
            }

            return (IMGUIContainer) visualElement; // lol
#endif
        }
    }
}