using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(AirshipTags))]
public class CustomTagEditor : UnityEditor.Editor {
    private string[] unityTags;
    SerializedProperty tagsProp;
    private ReorderableList list;

    private void OnEnable() {
        unityTags = InternalEditorUtility.tags;
        tagsProp = serializedObject.FindProperty("tags");
        list = new ReorderableList(serializedObject, tagsProp, true, true, true, true);
        list.drawHeaderCallback += DrawHeader;
        list.drawElementCallback += DrawElement;
        list.onAddDropdownCallback += OnAddDropdown;
    }

    private void DrawHeader(Rect rect) {
        EditorGUI.LabelField(rect, new GUIContent("Tags"), EditorStyles.boldLabel);
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
        var element = list.serializedProperty.GetArrayElementAtIndex(index);
        EditorGUI.LabelField(rect, element.stringValue);
    }

    private void OnAddDropdown(Rect buttonRect, ReorderableList list) {
        var config = GameConfig.Load();
        GenericMenu menu = new GenericMenu();

        for (int i = 0; i < config.tags.Count; i++) {
            var label = new GUIContent(config.tags[i]);
            // Don't allow duplicate tags to be added.

            if (PropertyContainsString(tagsProp, config.tags[i]))
                menu.AddDisabledItem(label, true);
            else
                menu.AddItem(label, false, OnAddClickHandler, config.tags[i]);
        }
        
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Edit Tags..."), false, data => {
            var tagEditor = EditorWindow.GetWindow<TagEditorWindow>();
            tagEditor.titleContent = new GUIContent("Tag Editor");
            tagEditor.Show();
        }, "");
        
        menu.ShowAsContext(); 
    }

    private bool PropertyContainsString(SerializedProperty property, string value)
    {
        if (property.isArray)
        {
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).stringValue == value)
                return true;
            }
        }
        else
            return property.stringValue == value;

        return false;
    }

    private void OnAddClickHandler(object tag) {
        int index = list.serializedProperty.arraySize;
        list.serializedProperty.arraySize++;
        list.index = index;

        var element = list.serializedProperty.GetArrayElementAtIndex(index);
        element.stringValue = (string)tag;
        serializedObject.ApplyModifiedProperties();
    }

    public override void OnInspectorGUI()
     {
         // GUILayout.Space(6);
         serializedObject.Update();
         list.DoLayoutList();
         serializedObject.ApplyModifiedProperties();
         // GUILayout.Space(3);
     }
}