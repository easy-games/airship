using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(TagManager))]
public class AirshipTagManagerEditor : UnityEditor.Editor {
    private bool showActiveTags = false;
    public override void OnInspectorGUI() {
        if (Application.isPlaying) {
            var tagManager = UnityEditor.Editor.FindAnyObjectByType<TagManager>();
            if (tagManager) {
                var tags = tagManager.GetAllTags();
                showActiveTags = EditorGUILayout.Foldout(true, "Active Tags");
                
                if (showActiveTags) {
                    foreach (var tag in tags) {
                        EditorGUILayout.LabelField(tag, EditorStyles.boldLabel);
                        var objects = tagManager.GetTagged(tag);
                        GUI.enabled = false;
                        foreach (var gameObject in objects) {
                            EditorGUILayout.ObjectField(gameObject, typeof(GameObject), false);
                        }
                        GUI.enabled = true;
                    }
                }
            }
        }
        else {
            EditorGUILayout.HelpBox("This component is used to manage tags for Airship - more debug info will be available at runtime", MessageType.Info);
        }
    }
}

[CustomEditor(typeof(AirshipTagReplicator))]
public class AirshipTagReplicatorEditor : UnityEditor.Editor {
    private bool showAttrs = true;
    public override void OnInspectorGUI() {
        var target = (AirshipTagReplicator) this.target;
        var networkObject = target.GetComponent<NetworkObject>();
        
        GUI.enabled = false;
        EditorGUILayout.ObjectField(new GUIContent("Airship Tags"), target.GetComponent<AirshipTags>(), typeof(AirshipTags),
            false);
        EditorGUILayout.ObjectField(new GUIContent("Network Object"), networkObject, typeof(NetworkObject), false);
        GUI.enabled = true;
    }
}

[CustomEditor(typeof(AirshipTags))]
public class CustomTagEditor : UnityEditor.Editor {
    SerializedProperty tagsProp;
    private ReorderableList list;

    private void OnEnable() {
        tagsProp = serializedObject.FindProperty("tags");
        list = new ReorderableList(serializedObject, tagsProp, !Application.isPlaying, true, true, true);
        list.multiSelect = true;
        list.drawHeaderCallback += DrawHeader;
        list.drawElementCallback += DrawElement;
        list.onAddDropdownCallback += OnAddDropdown;

        if (Application.isPlaying) {
            // at runtime we need to do some different logic to invoke events correctly
            list.onRemoveCallback += OnRemoveAtRuntime;
        }
    }

    private void OnRemoveAtRuntime(ReorderableList reorderableList) {
        List<string> tagsToRemove = new();
        foreach (var selectedIndex in reorderableList.selectedIndices) {
            var element = list.serializedProperty.GetArrayElementAtIndex(selectedIndex);
            tagsToRemove.Add(element.stringValue);
        }
        
        var target = (AirshipTags) this.target;
        foreach (var tag in tagsToRemove) {
            target.RemoveTag(tag);
        }
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
        if (Application.isPlaying) {
            // at runtime we need to do some different logic to invoke events correctly
            var target = (AirshipTags) this.target;
            target.AddTag((string) tag);
        }
        else {
            int index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;
            
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.stringValue = (string)tag;
            serializedObject.ApplyModifiedProperties();
        }
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