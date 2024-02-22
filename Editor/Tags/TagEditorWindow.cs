using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class TagEditorWindow : EditorWindow {
    [MenuItem("Airship/Tag Editor")]
    public static void ShowExample()
    {
        TagEditorWindow wnd = GetWindow<TagEditorWindow>();
        wnd.titleContent = new GUIContent("Tag Editor");
        wnd.name = "Tag Editor";
    }


    private ReorderableList list;
    private GameConfig config;
    
    private void OnEnable() {
        config = GameConfig.Load();
        list = new ReorderableList(config.tags, typeof(string), true, true, true ,true);
        list.drawElementCallback += DrawElement;
        list.drawHeaderCallback += DrawHeader;
        
        list.onReorderCallback += ListOnReorderCallback;
        list.onAddCallback += ListOnAddCallback;
        list.onRemoveCallback += ListOnRemoveCallback;
    }

    private void ListOnRemoveCallback(ReorderableList reorderableList) {
        if (reorderableList.selectedIndices.Count > 0) {
            foreach (var selected in reorderableList.selectedIndices) {
                config.tags.RemoveAt(selected);
            }
        }
        else {
            config.tags.Remove(config.tags.Last());
        }
        
   
        EditorUtility.SetDirty(config);
    }

    private void ListOnAddCallback(ReorderableList reorderableList) 
    {
        list.list.Add("NewTag" + (reorderableList.list.Count + 1));
        EditorUtility.SetDirty(config);
    }

    private void ListOnReorderCallback(ReorderableList reorderableList) {
        EditorUtility.SetDirty(config);
    }

    private void DrawHeader(Rect rect) {
        EditorGUI.LabelField(rect, new GUIContent("Game Tags"), EditorStyles.boldLabel);
    }

    private void DrawElement(Rect rect, int index, bool isactive, bool isfocused) {
        var list = this.list.list;
        var previousValue = list[index];

        if (Application.isPlaying) {
            EditorGUI.LabelField(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), (string) list[index]);
        }
        else {
            list[index] = EditorGUI.TextField(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), (string) list[index]);

            if (list[index] != previousValue) {
                EditorUtility.SetDirty(config);
            }
        }
        

    }

    private void OnGUI() {
        int uniformPadding = 5;
        RectOffset padding = new RectOffset(uniformPadding, uniformPadding, uniformPadding, uniformPadding);
        //Builds Layout Area from padding values
        Rect area = new Rect(padding.right, padding.top, position.width - (padding.right + padding.left), position.height - (padding.top + padding.bottom));
     
        GUILayout.BeginArea(area);
        list.displayAdd = !Application.isPlaying;
        list.displayRemove = !Application.isPlaying;
        list.draggable = !Application.isPlaying;
        list.DoLayoutList();
        
        GUILayout.EndArea();
    }
}
