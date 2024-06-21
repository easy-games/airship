#if UNITY_EDITOR
using System;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.Luau {
    public static class AirshipScriptGUI {
        internal delegate Object AirshipBehaviourValidator(Object[] references, SerializedProperty property);

        private static Object Validate(Object[] references, BinaryFile script, ScriptBinding binding) {
            if (references.Length <= 0) return null;
            
            var objectReference = references[0];
            if (objectReference is GameObject gameObject) {
                references = gameObject.GetComponents(typeof(ScriptBinding));
                foreach (var reference in references) {
                    if (reference != null && reference is ScriptBinding bindingComponent &&
                        bindingComponent.IsBindableAsComponent(script)) {
                        return reference;
                    }
                }
            }
            else if (objectReference is ScriptBinding otherBinding && otherBinding.IsBindableAsComponent(script)) {
                return otherBinding;
            }
            
            return null;
        }
        
        private static ScriptBinding DoAirshipBehaviourField(
            Rect position, 
            Rect dropRect, 
            int id, 
            BinaryFile script, 
            [CanBeNull] ScriptBinding scriptBinding, 
            SerializedProperty property,
            bool allowSceneObjects,
            Action<ScriptBinding> onObjectSelected = null,
            Action onObjectRemoved = null) {
            Event evt = Event.current;
            EventType eventType = evt.type;

            var obj = (Object) property?.objectReferenceValue ?? scriptBinding;

            if (eventType == EventType.ContextClick && position.Contains(Event.current.mousePosition)) {
                var contextMenu = new GenericMenu();
                
                if (property != null) {
                    contextMenu.AddItem(new GUIContent("Properties..."), false, () => {
                        EditorUtility.OpenPropertyEditor(obj);
                    });
                }
                
                contextMenu.DropDown(position);
                Event.current.Use();
            }
            
            var buttonStyle = new GUIStyle("ObjectFieldButton");
            var buttonRect =
                buttonStyle.margin.Remove(new Rect(position.xMax - 19, position.y, 19, position.height));

            switch (eventType) {
                case EventType.DragUpdated:
                case EventType.DragPerform: {
                    // Handle drag over lol

                    if (dropRect.Contains(Event.current.mousePosition) && GUI.enabled) {
                        // if dropping something on this
                        var references = DragAndDrop.objectReferences;

                        var validatedObject = Validate(references, script, scriptBinding);
                        if (validatedObject != null) {
                            if (!allowSceneObjects && !EditorUtility.IsPersistent(validatedObject)) {
                                validatedObject = null;
                            }

                            if (DragAndDrop.visualMode == DragAndDropVisualMode.None)
                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                            if (eventType == EventType.DragPerform) {
                                obj = validatedObject;

                                GUI.changed = true;
                                DragAndDrop.AcceptDrag();
                                DragAndDrop.activeControlID = 0;
                            }
                            else {
                                DragAndDrop.activeControlID = id;
                            }

                            Event.current.Use();
                        }
                    }

                    break;
                }
                case EventType.MouseDown: {
                    if (buttonRect.Contains(Event.current.mousePosition)) {
                        if (GUI.enabled) {
                            GUIUtility.keyboardControl = id;

                            var searchContext = SearchService.CreateContext(string.Empty);
                            var view = SearchService.ShowPicker(
                                searchContext,
                                (item, b) => {
                                    var obj = item.ToObject<ScriptBinding>();
                                    if (obj != null) {
                                        onObjectSelected?.Invoke(obj);
                                    }

                                    GUI.changed = true;
                                },
                                item => {
                                    var obj = item.ToObject<ScriptBinding>();
                                    if (obj != null) {
                                        EditorGUIUtility.PingObject(obj);
                                    }
                                },
                                item => {
                                    var itemScriptBinding = item.ToObject<ScriptBinding>();
                                    return itemScriptBinding != null &&
                                           itemScriptBinding.IsBindableAsComponent(script);
                                }, null, script.m_metadata?.displayName ?? "AirshipBehaviour");
                            view.SetSearchText($"h:t:ScriptBinding"); // #m_fileFullPath={script.m_path}

                            evt.Use();
                            GUIUtility.ExitGUI();
                        }
                    }
                    else if (Event.current.button == 0 && position.Contains(Event.current.mousePosition)) {
                        var actualTarget = property != null ? property.objectReferenceValue : obj;
                        var component = actualTarget as ScriptBinding;
                        if (component) {
                            actualTarget = component.gameObject;
                        }
                        
                        switch (Event.current.clickCount) {
                            case 1:
                                EditorGUIUtility.PingObject(actualTarget);
                                evt.Use();
                                break;
                            case 2: {
                                if (actualTarget) {
                                    AssetDatabase.OpenAsset(actualTarget);
                                    evt.Use();
                                    GUIUtility.ExitGUI();
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
                case EventType.Repaint: {
                    var temp = EditorGUIUtility.ObjectContent(obj, typeof(ScriptBinding));
                    
                    temp.text = obj == null
                        ? $"None ({script.m_metadata?.displayName ?? script.m_path})"
                        : $"{obj.name} ({script.m_metadata?.displayName ?? script.m_path})";

                    if (script.m_metadata?.displayIcon != null) {
                        temp.image = script.m_metadata.displayIcon;
                    }
                    
                    EditorStyles.objectField.Draw(position, temp, id, DragAndDrop.activeControlID == id,
                        position.Contains(Event.current.mousePosition));



                    buttonStyle.Draw(buttonRect, GUIContent.none, id, DragAndDrop.activeControlID == id,
                        buttonRect.Contains(Event.current.mousePosition));

                    break;
                }
                case EventType.KeyDown: {
                    if (GUIUtility.keyboardControl == id) {
                        if (evt.keyCode == KeyCode.Backspace || (evt.keyCode == KeyCode.Delete &&
                                                                 (evt.modifiers & EventModifiers.Shift) == 0)) {
                            obj = null;
                        }
                        
                        GUI.changed = true;
                        evt.Use();
                    }
                    break;
                }
            }

            return (ScriptBinding) obj;
        }
        
        internal static ScriptBinding AirshipBehaviourField(Rect rect, GUIContent content, BinaryFile script, ScriptBinding scriptBinding, SerializedProperty property) {
            int id = GUIUtility.GetControlID("_airshipBehaviourFieldHash".GetHashCode(), FocusType.Keyboard, rect);
            
            rect = EditorGUI.PrefixLabel(rect, id, content);
            var value = DoAirshipBehaviourField(
                rect, rect, id, script, scriptBinding, property, true,
                binding => {
                    if (property != null) {
                        property.objectReferenceValue = binding;
                    }
                },
                () => {
                    Debug.Log("Remove object " + property.objectReferenceValue);
                    property.objectReferenceValue = null;
                });
            
            return value;
        }

        internal static ScriptBinding AirshipBehaviourField(GUIContent content, BinaryFile script, SerializedProperty property) {
            var r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, script, (ScriptBinding) property.objectReferenceValue, property);
        }
        
        public static ScriptBinding AirshipBehaviourField(GUIContent content, BinaryFile script, ScriptBinding scriptBinding) {
            var r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, script, scriptBinding, null);
        }
    }
}
#endif