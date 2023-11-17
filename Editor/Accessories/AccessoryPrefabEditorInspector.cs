using System;
using Code.Player.Accessories.Editor;
using ParrelSync;
using UnityEditor;
using UnityEngine;

namespace Editor.Accessories {
    [CustomEditor(typeof(AccessoryPrefabEditor))]
    public class AccessoryPrefabEditorInspector : UnityEditor.Editor {
        private void OnEnable() {
        }
        private void OnDisable() {
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            // Add the Open Editor button:
            EditorGUILayout.Space();
            if (ClonesManager.IsClone()) {
                GUILayout.Label("Accessory Editor disabled in clone window.");
            } else {
                if (GUILayout.Button("Open Editor")) {
                    AccessoryEditorWindow.OpenOrCreateWindow();
                    // var accessory = targets?.First((obj) => obj is Accessory) as Accessory;
                    // var accessory = target as AccessoryPrefabEditor;
                    // if (accessory != null) {
                    //     AccessoryEditor.OpenWithAccessory(accessory);
                    // }
                }
            }
        }
    }
}
