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
    
    private void OnEnable() {
        var config = GameConfig.Load();
        list = new ReorderableList(config.tags, typeof(string), true, true, true ,true);
        list.drawElementCallback += DrawElement;
        list.drawHeaderCallback += DrawHeader;
    }

    private void DrawHeader(Rect rect) {
        EditorGUI.LabelField(rect, new GUIContent("Game Tags"), EditorStyles.boldLabel);
    }

    private void DrawElement(Rect rect, int index, bool isactive, bool isfocused) {
        list.list[index] = EditorGUI.TextField(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), (string) list.list[index]);
    }

    private void OnGUI() {
        int uniformPadding = 5;
        RectOffset padding = new RectOffset(uniformPadding, uniformPadding, uniformPadding, uniformPadding);
        //Builds Layout Area from padding values
        Rect area = new Rect(padding.right, padding.top, position.width - (padding.right + padding.left), position.height - (padding.top + padding.bottom));
     
        GUILayout.BeginArea(area);
        list.DoLayoutList();
        GUILayout.EndArea();
    }
}
