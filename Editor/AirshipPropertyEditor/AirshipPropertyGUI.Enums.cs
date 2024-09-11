using System.Collections.Generic;
using System.Globalization;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.AirshipPropertyEditor {
    internal static partial class AirshipPropertyGUI {
        private static void DrawCustomStringEnumDropdown(GUIContent content, TypeScriptEnum enumerableType,
            SerializedProperty value, SerializedProperty modified, Rect? drawRect) {
                    
            if (enumerableType.members.Count == 0) {
                GUI.enabled = false;

                if (drawRect.HasValue) {
                    EditorGUI.Popup(drawRect.Value, content, 0, new GUIContent[] { new GUIContent("(No Values)") });
                }
                else {
                    EditorGUILayout.Popup(content, 0, new GUIContent[] { new GUIContent("(No Values)") });
                }
                
                GUI.enabled = true;
                return;
            }
            
            List<GUIContent> items = new();
            foreach (var item in enumerableType.members) {
                items.Add(new GUIContent(ObjectNames.NicifyVariableName(item.Name)));
            }
            
            int idx = enumerableType.members.FindIndex(f => f.StringValue == value.stringValue);
            if (idx == -1) {
                idx = 0;
            }
            
            idx = drawRect.HasValue ? EditorGUI.Popup(drawRect.Value, content, idx, items.ToArray()) : EditorGUILayout.Popup(content, idx, items.ToArray());
            string newValue = enumerableType.members[idx].StringValue;
            
            if (newValue != value.stringValue) {
                value.stringValue = newValue;
                modified.boolValue = true;
            }
        }
    
        private static void DrawCustomIntEnumDropdown(GUIContent content, TypeScriptEnum enumerableType, SerializedProperty value, SerializedProperty modified, Rect? drawRect) {
            if (enumerableType == null) {
                return;
            }
            
            if (enumerableType.members.Count == 0) {
                GUI.enabled = false;

                if (drawRect.HasValue) {
                    EditorGUI.Popup(drawRect.Value, content,0, new GUIContent[] { new GUIContent("(No Values)") });
                }
                else {
                    EditorGUILayout.Popup( content,0, new GUIContent[] { new GUIContent("(No Values)") });
                }
                
                
                GUI.enabled = true;
                return;
            }
            
            List<GUIContent> items = new();
            foreach (var item in enumerableType.members) {
                items.Add(new GUIContent(ObjectNames.NicifyVariableName(item.Name) + " [" + item.IntValue + "]") );
            }
                
            int idx = 0;

            int.TryParse(value.stringValue, out int currentValue);
            
            int targetIdx = enumerableType.members.FindIndex(f => f.IntValue == currentValue);
            idx = targetIdx != -1 ? targetIdx : 0;
            
                
            idx = drawRect.HasValue ? EditorGUI.Popup(drawRect.Value, content, idx, items.ToArray()) : EditorGUILayout.Popup(content, idx, items.ToArray());
            string newValue = enumerableType.members[idx].IntValue.ToString(CultureInfo.InvariantCulture);
            
            if (newValue != value.stringValue) {
                value.stringValue = newValue;
                modified.boolValue = true;
            }
        }

        internal static void DrawIntEnumProperty(GUIContent guiContent, LuauMetadataProperty metadataProperty,
            SerializedProperty value, SerializedProperty modified) {
            //
            if (!AirshipEditorInfo.Instance) return;

            if (metadataProperty.refPath == null) {
                return;
            }
            
            var tsEnum = AirshipEditorInfo.Enums.GetEnum(metadataProperty.refPath);
            if (tsEnum == null) return;

            DrawCustomIntEnumDropdown(guiContent, tsEnum, value, modified, null);
        }
        
        internal static void DrawStringEnumProperty(GUIContent guiContent, LuauMetadataProperty metadataProperty, SerializedProperty value,
            SerializedProperty modified) {
            if (!AirshipEditorInfo.Instance) return;
            
            var tsEnum = AirshipEditorInfo.Enums.GetEnum(metadataProperty.refPath);
            if (tsEnum == null) return;

            DrawCustomStringEnumDropdown(guiContent, tsEnum, value, modified, null);
        }
    }
}