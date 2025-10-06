
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class AirshipObjectGUIInternal {
    // Unfortunately this is necessary due to lack of customization

    public delegate void ObjectSelectAction(UnityEngine.Object obj, System.Type[] requiredTypes);
    
    private static Object DoCustomObjectField(
        Rect position, 
        Rect dropRect, 
        int id, 
        Object obj, 
        Object objBeingEdited, 
        Type objType,
        EditorGUI.ObjectFieldValidator validator,
        bool allowSceneObjects,
        GUIStyle style,
        GUIStyle buttonStyle,
        ObjectSelectAction onRequestSelectObject
        ) {

        var visualType = EditorGUI.ObjectFieldVisualType.IconAndText;
        validator ??= EditorGUI.ValidateObjectFieldAssignment;
        
        var current = Event.current;
        var eventType = current.type;
        var buttonRect = new Rect(position.xMax - 19f, position.y, 19f, position.height);
        
        if (eventType == EventType.ContextClick && position.Contains(Event.current.mousePosition))
        {
            var actualObject = obj;
            var menu = new GenericMenu();
            
            // if (EditorGUI.FillPropertyContextMenu(null, menu: menu) != null) {
            //     menu.AddSeparator("");
            // }
            
            menu.AddItem(new GUIContent("Properties..."), false, () => PropertyEditor.OpenPropertyEditor(actualObject));
            menu.DropDown(position);
            Event.current.Use();
        }

        switch (eventType) {
            case EventType.KeyDown: {
                if (GUIUtility.keyboardControl == id) {
                    if (current.keyCode == KeyCode.Backspace || current.keyCode == KeyCode.Delete &&
                        (current.modifiers & EventModifiers.Shift) == EventModifiers.None) {
                        obj = null;
                        GUI.changed = true;
                        current.Use();
                    }
                    if (current.MainActionKeyForControl(id)) {
                        onRequestSelectObject(obj, new[] { objType });
                        current.Use();
                        GUIUtility.ExitGUI();
                    }
                }
                
                break;
            }
            case EventType.MouseDown: {
                if (position.Contains(Event.current.mousePosition) &&
                    Event.current.button == 0) {

                    EditorGUIUtility.editingTextField = false;
                    if (buttonRect.Contains(Event.current.mousePosition)) {
                        if (GUI.enabled) {
                            GUIUtility.keyboardControl = id;
                            
                            onRequestSelectObject(obj, new[] { objType });
                            
                            current.Use();
                            GUIUtility.ExitGUI();
                        }
                    } else {
                        if (Event.current.clickCount == 1) {
                            GUIUtility.keyboardControl = id;
                            EditorGUI.PingObjectOrShowPreviewOnClick(obj, position);
                            current.Use();
                        }  else if (Event.current.clickCount == 2 && (bool) obj)
                        {
                            AssetDatabase.OpenAsset(obj);
                            current.Use();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                break;
            }
            case EventType.Repaint: {
                var content = EditorGUIUtility.ObjectContent(obj, objType, null, validator);
                var contentColor = GUI.contentColor;
                var a = contentColor.a;

                try {
                    if (obj == null)
                    {
                        contentColor.a = 0.7f;
                        GUI.contentColor = contentColor;
                    }
                    
                    EditorGUI.BeginHandleMixedValueContentColor();
                    style.Draw(position, content, id, DragAndDrop.activeControlID == id, position.Contains(UnityEngine.Event.current.mousePosition));
                    var position1 = buttonStyle.margin.Remove(buttonRect);
                    buttonStyle.Draw(position1, GUIContent.none, id, DragAndDrop.activeControlID == id, position1.Contains(UnityEngine.Event.current.mousePosition));
                    EditorGUI.EndHandleMixedValueContentColor();
                } finally {
                    contentColor.a = a;
                    GUI.contentColor = contentColor;
                }
                
                break;
            }
            case EventType.DragUpdated:
            case EventType.DragPerform: {
                if (dropRect.Contains(UnityEngine.Event.current.mousePosition) && GUI.enabled) {
                    var objectReferences = DragAndDrop.objectReferences;
                    var target = validator(objectReferences, objType, null, EditorGUI.ObjectFieldValidatorOptions.None);
                    if (target != null && !allowSceneObjects && !EditorUtility.IsPersistent(target)) {
                        target = null;
                    }
                    if (target != null) {
                        if (DragAndDrop.visualMode == DragAndDropVisualMode.None)
                            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;

                        if (eventType == EventType.DragPerform) {
                            obj = target;
                            GUI.changed = true;
                            DragAndDrop.AcceptDrag();
                            DragAndDrop.activeControlID = 0;
                        } else DragAndDrop.activeControlID = id;
                        current.Use();
                    }
                }

                break;
            }
            case EventType.DragExited:
                if (GUI.enabled) HandleUtility.Repaint();
                break;
        }

        return obj;
    }

    public static GUIStyle objectFieldButtonStyle => EditorStyles.objectFieldButton;
    
    public static Object DoObjectField(
        Rect position,
        Rect dropRect,
        GUIContent label,
        int id,
        Object obj,
        Object objBeingEdited,
        Type objType,
        EditorGUI.ObjectFieldValidator validator,
        bool allowSceneObjects,
        GUIStyle style,
        GUIStyle buttonStyle, 
        Action<Object> onObjectSelectorClosed = null,
        Action<Object> onObjectSelectedUpdated = null) {
        
        var controlId = GUIUtility.GetControlID(id, FocusType.Keyboard, position);
        position = EditorGUI.PrefixLabel(position, controlId, label);
        
        void ShowObjectSelector(Object onObject, Type[] types) {
            ObjectSelector.get.Show(onObject, types, onObject, allowSceneObjects, onObjectSelectedUpdated: onObjectSelectedUpdated, onObjectSelectorClosed: onObjectSelectorClosed);
            ObjectSelector.get.objectSelectorID = controlId;
        }

        var result = DoCustomObjectField(position, dropRect, controlId, obj, objBeingEdited, objType, validator, allowSceneObjects,
            style, buttonStyle, ShowObjectSelector);

        var current = Event.current;
        var commandName = current.commandName;
        
        switch (Event.current.type) {
            case EventType.ExecuteCommand: {
                if (commandName == "ObjectSelectorUpdated" && ObjectSelector.get.objectSelectorID == controlId) {
                    return ObjectSelector.GetCurrentObject();
                }
                if (commandName == "ObjectSelectorClosed" && ObjectSelector.get.objectSelectorID == controlId)
                {
                    if (ObjectSelector.get.GetInstanceID() != 0)
                        return ObjectSelector.GetCurrentObject();
                    current.Use();
                }
                if ((current.commandName == "Delete" || current.commandName == "SoftDelete") &&
                    GUIUtility.keyboardControl == id) {
                    obj = null;
                    
                    GUI.changed = true;
                    current.Use();
                }
                break;
            }
        }

        return result;
    }
}
