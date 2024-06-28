using System.Collections.Generic;
using System.Linq;
using Editor.Accessories;
using ParrelSync;
using UnityEditor;
using UnityEngine;

namespace Code.Player.Accessories.Editor {
    /// <summary>
    /// Adds an "Open Editor" button to Accessory items, which will open the
    /// Accessory Editor window when clicked.
    /// </summary>
    [CustomEditor(typeof(AccessoryComponent))]
    public class AccessoryInspector : UnityEditor.Editor {
        private bool foldout = false; // Variable to handle foldout state
        private void OnEnable() {
            var accessoryComponent = (AccessoryComponent)target;
            foldout = accessoryComponent.bodyMask > 0;
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            // Add the Open Editor button:
            EditorGUILayout.Space();
            if (RunCore.IsClone()) {
                GUILayout.Label("Accessory Editor disabled in clone window.");
                return;
            }
            if (GUILayout.Button("Open Editor")) {
                var accessory = targets?.First((obj) => obj is AccessoryComponent) as AccessoryComponent;
                if (accessory != null) {
                    AccessoryEditorWindow.OpenWithAccessory(accessory);
                }
            }

            // Start a foldout
            EditorGUILayout.Space();
            foldout = EditorGUILayout.Foldout(foldout, "Hide Body Parts");

            if (foldout) {
                EditorGUI.indentLevel++;

                // Show bools for all the hide bits
                var accessoryComponent = (AccessoryComponent)target;
                int hideBits = accessoryComponent.bodyMask;

                // Display them based on the sort order in BodyMaskInspectorData
                foreach (var maskData in AccessoryComponent.BodyMaskInspectorDatas) {
                    if (maskData.bodyMask == AccessoryComponent.BodyMask.NONE) {
                        continue;
                    }

                    bool isHidden = (hideBits & (int)maskData.bodyMask) != 0;
                    bool newIsHidden = EditorGUILayout.Toggle(maskData.name, isHidden);
                    if (newIsHidden != isHidden) {
                        if (newIsHidden) {
                            hideBits |= (int)maskData.bodyMask;
                        }
                        else {
                            hideBits &= ~(int)maskData.bodyMask;
                        }
                    }
                }
                accessoryComponent.bodyMask = hideBits;

                EditorGUI.indentLevel--;
            }
        }

        [MenuItem("Airship/Misc/Prefab Tools/Accessory Editor")]
        public static void OpenAccessoryEditor() {
            AccessoryEditorWindow.OpenOrCreateWindow();
        }
    }
}
