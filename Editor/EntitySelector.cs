using Airship.Editor;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;

[InitializeOnLoad]
public static class EntitySelector {
    static EntitySelector() {
        ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
    }

    private static void OnToolbarGUI() {
        if (!Application.isPlaying || ClonesManager.IsClone()) return;
        
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Select Local Entity", "Select the local entity"),
                ToolbarStyles.PackagesButtonStyle)) {
            var entityDrivers = Object.FindObjectsOfType<EntityDriver>();
            foreach (var entityDriver in entityDrivers) {
                if (entityDriver.IsOwner) {
                    Selection.activeGameObject = entityDriver.gameObject;
                    break;
                }
            }
        }
    }
}
