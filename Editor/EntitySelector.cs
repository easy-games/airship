using Airship.Editor;
using Code.Player.Character;
using Code.Player.Human;
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
        if (!Application.isPlaying || !RunCore.IsClient()) return;
        
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Select Local Entity", "Select the local entity"),
                ToolbarStyles.LocalEntityButtonStyle)) {
            var entityDrivers = Object.FindObjectsByType<CharacterMovement>(FindObjectsSortMode.None);
            foreach (var entityDriver in entityDrivers) {
                if (entityDriver.IsOwner) {
                    Selection.activeGameObject = entityDriver.gameObject;
                    break;
                }
            }
        }
    }
}
