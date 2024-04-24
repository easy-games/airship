using System;
using Luau;
using UnityEditor;
using UnityEditor.Search;
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

        internal static Object Validate(Object[] references, string[] validPaths) {
            if (references.Length > 0) {
                
            }

            return null;
        }
        
        private static ScriptBinding DoAirshipBehaviourField(Rect position, Rect dropRect, int id, ScriptBinding scriptBinding, bool allowSceneObjects, GUIStyle style = null) {
            Event evt = Event.current;
            EventType eventType = evt.type;

            switch (eventType) {
                case EventType.DragUpdated:
                case EventType.DragPerform: {
                    // Handle drag over lol

                    if (dropRect.Contains(Event.current.mousePosition) && GUI.enabled) {
                        // if dropping something on this
                        var references = DragAndDrop.objectReferences; // validate objs
                        // Object validatedObject = Validate(references, null);
                    }
                    break;
                }
            }

            return null;
        }
        
        internal static ScriptBinding AirshipBehaviourField(Rect rect, GUIContent content, ScriptBinding scriptBinding, LuauMetadataProperty property) {
            int id = GUIUtility.GetControlID(0, FocusType.Keyboard, rect);
            return DoAirshipBehaviourField(rect, rect, "_airshipBehaviourFieldHash".GetHashCode(), scriptBinding, true);
        }

        public static ScriptBinding AirshipBehaviourField(GUIContent content, ScriptBinding scriptBinding, LuauMetadataProperty property) {
            Rect r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, scriptBinding, property);
        }
    }
}