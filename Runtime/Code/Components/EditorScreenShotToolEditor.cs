#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Code.Components {
    [CustomEditor(typeof(EditorScreenShotTool))]
    public class EditorScreenShotToolEditor : Editor {
        public override void OnInspectorGUI() {
            // Draw default inspector first
            DrawDefaultInspector();

            EditorScreenShotTool tool = (EditorScreenShotTool)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Take Screenshot")) {
                if (tool.transparentBackground) {
                    tool.TakeScreenshotRenderTransparent();
                } else {
                    tool.TakeScreenshotRender();
                }
            }
        }
    }
}
#endif