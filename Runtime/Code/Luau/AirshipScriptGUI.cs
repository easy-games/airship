using System;
using Luau;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Code.Luau {
    // public class AirshipBehaviourField : BaseField<ScriptBinding> {
    //     public AirshipBehaviourField(string label, VisualElement visualInput) : base(label, visualInput) {
    //     }
    // }
    
    public static class AirshipScriptGUI {
        internal delegate Object AirshipBehaviourValidator(Object[] references, SerializedProperty property);

        private static Object Validate(Object[] references, BinaryFile script, ScriptBinding binding) {
            if (references.Length <= 0) return null;
            
            var objectReference = references[0];
            if (objectReference is not GameObject gameObject) return null;
                
            references = gameObject.GetComponents(typeof(ScriptBinding));
            foreach (var reference in references) {
                if (reference != null && reference is ScriptBinding bindingComponent &&
                    bindingComponent.m_fileFullPath == script.m_path) {
                    return reference;
                }
            }

            return null;
        }
        
        private static ScriptBinding DoAirshipBehaviourField(
            Rect position, 
            Rect dropRect, 
            int id, 
            BinaryFile script, 
            ScriptBinding scriptBinding, 
            bool allowSceneObjects, 
            GUIStyle style = null
            ) {
            Event evt = Event.current;
            EventType eventType = evt.type;

            switch (eventType) {
                case EventType.DragUpdated:
                case EventType.DragPerform: {
                    // Handle drag over lol

                    if (dropRect.Contains(Event.current.mousePosition) && GUI.enabled) {
                        // if dropping something on this
                        var references = DragAndDrop.objectReferences;
                        
                        
                        Object validatedObject = Validate(references, script, scriptBinding);
                        if (validatedObject != null) {
                            Debug.Log($"validating object {references.Length}");
                            if (!allowSceneObjects && !EditorUtility.IsPersistent(validatedObject)) {
                                validatedObject = null;
                            }

                            if (DragAndDrop.visualMode == DragAndDropVisualMode.None)
                                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                            
                            if (eventType == EventType.DragPerform) {
                                
                                GUI.changed = true;
                                DragAndDrop.AcceptDrag();
                                DragAndDrop.activeControlID = 0;
                            }
                            else
                            {
                                DragAndDrop.activeControlID = id;
                            }
                            Event.current.Use();
                        }
                    }
                    break;
                }
                case EventType.MouseDown: {
                    if (position.Contains(Event.current.mousePosition)) {
                        if (GUI.enabled) {
                            // GUIUtility.keyboardControl = id;
                            //     // ??
                            //
                            //    UnityEditor.Search.Object
                            //    item.
                            //
                            // evt.Use();
                            // GUIUtility.ExitGUI();
                        }
                    }

                    break;
                }
                case EventType.Repaint: {
                    GUIContent temp = EditorGUIUtility.ObjectContent(scriptBinding, typeof(ScriptBinding));
                    temp.text = scriptBinding == null ? $"None ({script.m_metadata?.displayName ?? script.m_path})" : $"{scriptBinding.name} ({script.m_metadata?.displayName ?? script.m_path})";
                    
                    EditorStyles.objectField.Draw(position, temp, id, DragAndDrop.activeControlID == id, position.Contains(Event.current.mousePosition));

                    var buttonStyle = new GUIStyle("ObjectFieldButton");
                    var buttonRect =
                        buttonStyle.margin.Remove(new Rect(position.xMax - 19, position.y, 19, position.height));
                    buttonStyle.Draw(buttonRect, GUIContent.none, id, DragAndDrop.activeControlID == id, buttonRect.Contains(Event.current.mousePosition));
                    
                    break;
                }
            }

            return scriptBinding;
        }
        
        internal static ScriptBinding AirshipBehaviourField(Rect rect, GUIContent content, BinaryFile script, ScriptBinding scriptBinding) {
            int id = GUIUtility.GetControlID("_airshipBehaviourFieldHash".GetHashCode(), FocusType.Keyboard, rect);
            
            rect = EditorGUI.PrefixLabel(rect, id, content);
            var value = DoAirshipBehaviourField(rect, rect, id, script, scriptBinding, true);
            
            return value;
        }

        public static ScriptBinding AirshipBehaviourField(GUIContent content, BinaryFile script, ScriptBinding scriptBinding) {
            Rect r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, script, scriptBinding);
        }
    }
}