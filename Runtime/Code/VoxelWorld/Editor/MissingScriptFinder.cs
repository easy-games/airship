using System.Collections.Generic;
using UnityEditor;

using UnityEngine;

public class MissingScriptFinder : EditorWindow {
    [MenuItem("Airship/Misc/Find and Remove Missing Scripts")]
    public static void ShowWindow() {
        GetWindow<MissingScriptFinder>("Find and Remove Missing Scripts");
    }

    private void OnGUI() {
        if (GUILayout.Button("Find and Remove Missing Scripts in Scene")) {
            FindAndRemoveMissingScripts();
        }
    }

    private static void FindAndRemoveMissingScripts() {
        int count = 0;

        // Check all GameObjects in the current scene
        GameObject[] gameObjects = FindObjectsOfType<GameObject>();
        count += RemoveMissingScriptsFromGameObjects(gameObjects);

        // Check all GameObjects in the current prefab stage, if any
        UnityEditor.SceneManagement.PrefabStage prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null) {
            GameObject prefabRoot = prefabStage.prefabContentsRoot;
            List<GameObject> prefabGameObjects = new List<GameObject>();
            CollectAllGameObjects(prefabRoot, prefabGameObjects);
            count += RemoveMissingScriptsFromGameObjects(prefabGameObjects.ToArray());
        }

        Debug.Log($"Removed {count} missing scripts.");
    }

    private static void CollectAllGameObjects(GameObject root, List<GameObject> gameObjects) {
        gameObjects.Add(root);
        foreach (Transform child in root.transform) {
            CollectAllGameObjects(child.gameObject, gameObjects);
        }
    }

    private static int RemoveMissingScriptsFromGameObjects(GameObject[] gameObjects) {
        int count = 0;

        foreach (GameObject go in gameObjects) {
            int initialCount = count;
            Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            SerializedObject serializedObject = new SerializedObject(go);
            SerializedProperty prop = serializedObject.FindProperty("m_Component");

            for (int i = prop.arraySize - 1; i >= 0; i--) {
                SerializedProperty component = prop.GetArrayElementAtIndex(i);
                if (component.objectReferenceValue == null) {
                    prop.DeleteArrayElementAtIndex(i);
                    count++;
                }
            }

            serializedObject.ApplyModifiedProperties();

            //count += componentsToRemove.Count;
        }

        return count;
    }
}
