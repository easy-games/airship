using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Editor.EditorInternal {
    public class AirshipEditorInternals {
        internal static bool SetBoldDefaultFont(bool isBold) {
            var wasBold = EditorGUIUtility.GetBoldDefaultFont();
            EditorGUIUtility.SetBoldDefaultFont(isBold);
            return wasBold;
        }

        internal static bool GetBoldDefaultFont() {
            return EditorGUIUtility.GetBoldDefaultFont();
        }

        internal static void ShowObjectSelector(UnityEngine.Object obj, Type objectTypes, UnityEngine.Object objBeingEdited, bool allowSceneObjects) {
            ObjectSelector.get.Show(obj, objectTypes,objBeingEdited, allowSceneObjects);
        }

        internal static UnityEngine.Object DoCustomObjectField(Rect position, GUIContent label,
            UnityEngine.Object value, System.Type type) {
            int id = "s_ObjectField".GetHashCode();
            var eventType = Event.current;

            position = EditorGUI.PrefixLabel(position, label);

            switch (eventType.type) {
                case EventType.MouseDown: {
                    if (position.Contains(UnityEngine.Event.current.mousePosition) &&
                        UnityEngine.Event.current.button == 0) {
                        // Rect buttonRect = EditorGUI.GetButtonRect(visualType, position);
                        EditorGUIUtility.editingTextField = false;
                        if (GUI.enabled) {
                            ShowObjectSelector(value, type, null, true);
                        }
                    }

                    break;
                }
                case EventType.Repaint: {
                    EditorGUI.BeginHandleMixedValueContentColor();

                    GUIContent content = EditorGUIUtility.ObjectContent(value, type, false);
                    Color contentColor = GUI.contentColor;
                    float a = contentColor.a;

                    try {
                        if (value == null) {
                            contentColor.a = 0.7f;
                            GUI.contentColor = contentColor;
                        }

                        var buttonRect = new Rect(position.xMax - 19f, position.y, 19f, position.height);
                        Rect position1 = EditorStyles.objectFieldButton.margin.Remove(buttonRect);
                        EditorStyles.objectField.Draw(position, content, id, DragAndDrop.activeControlID == id,
                            position.Contains(UnityEngine.Event.current.mousePosition));
                        EditorStyles.objectFieldButton.Draw(position1, GUIContent.none, id,
                            DragAndDrop.activeControlID == id,
                            position1.Contains(UnityEngine.Event.current.mousePosition));

                        EditorGUI.EndHandleMixedValueContentColor();
                        break;
                    } finally {
                        contentColor.a = a;
                        GUI.contentColor = contentColor;
                    }
                }
            }

            return null;
        }
    }
}