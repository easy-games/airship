using ParrelSync;
using UnityEditor;
using UnityEngine;

namespace Editor.Accessories {
    [CustomEditor(typeof(AccessoryPrefabEditor))]
    public class AccessoryPrefabEditorInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            // Add the Open Editor button:
            EditorGUILayout.Space();
            if (ClonesManager.IsClone()) {
                GUILayout.Label("Accessory Editor disabled in clone window.");
            } else {
                if (GUILayout.Button("Open Editor")) {
                    AccessoryEditorWindow.OpenOrCreateWindow();
                }
            }
        }
    }
}
