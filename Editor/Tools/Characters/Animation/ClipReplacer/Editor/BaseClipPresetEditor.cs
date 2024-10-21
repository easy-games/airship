using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(BaseClipPreset))]
public class BaseClipPresetEditor : UnityEditor.Editor
{
    private ReorderableList clipSelectionList;

    private void OnEnable()
    {
        clipSelectionList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("baseClipSelections"),
                true, true, true, true);

        clipSelectionList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Base Clip Selections");
        };

        clipSelectionList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (index >= clipSelectionList.count)
            {
                Debug.LogError($"Index {index} is out of bounds. Array size: {clipSelectionList.count}");
                return;
            }

            var element = clipSelectionList.serializedProperty.GetArrayElementAtIndex(index);
            Rect elementRect = new Rect(rect.x, rect.y + 2.5f, rect.width - 20, EditorGUIUtility.singleLineHeight);
            Rect buttonRect = new Rect(rect.x + rect.width - 17.5f, rect.y + 2.5f, 20, EditorGUIUtility.singleLineHeight);

            if (element == null)
            {
                Debug.LogError($"Element at index {index} is null.");
                return;
            }

            EditorGUI.PropertyField(elementRect, element, GUIContent.none);

            if (GUI.Button(buttonRect, "x"))
            {
                serializedObject.Update();
                var arrayProperty = clipSelectionList.serializedProperty;
                if (index < arrayProperty.arraySize)
                {
                    arrayProperty.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    clipSelectionList.index = -1; 
                    Repaint();
                }
                else
                {
                    Debug.LogError($"Index {index} is out of bounds. Array size: {arrayProperty.arraySize}");
                }
            }
        };

        clipSelectionList.onAddCallback = (ReorderableList list) =>
        {
            var index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;

            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("clipName").stringValue = "";
        };

        clipSelectionList.onRemoveCallback = (ReorderableList list) =>
        {
            if (EditorUtility.DisplayDialog("Warning!",
                "Are you sure you want to delete the clip selection?", "Yes", "No"))
            {
                var index = list.index;
                var arrayProperty = list.serializedProperty;
                if (index < arrayProperty.arraySize)
                {
                    Debug.Log($"Removing element at index {index} from Remove Callback");
                    arrayProperty.DeleteArrayElementAtIndex(index); 
                    serializedObject.ApplyModifiedProperties();
                    clipSelectionList.index = -1;
                    Repaint();
                }
                else
                {
                    Debug.LogError($"Index {index} is out of bounds in Remove Callback. Array size: {arrayProperty.arraySize}");
                }
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BaseClipPreset preset = (BaseClipPreset)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorOverrideController"));

        if (GUILayout.Button("Load Base Clip Names"))
        {
            preset.LoadBaseClipNames();
        }

        if (GUILayout.Button("Clear All"))
        {
            preset.ClearAllSelections();
        }

        clipSelectionList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(preset);
        }
    }
}