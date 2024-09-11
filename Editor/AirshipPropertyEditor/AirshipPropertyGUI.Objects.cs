using Code.Luau;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.AirshipPropertyEditor {
    internal static partial class AirshipPropertyGUI {
        internal static void DrawAirshipComponentProperty(Object target, GUIContent guiContent, LuauMetadata metadata, LuauMetadataProperty metadataProperty, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty modified) {
            var currentObject = (AirshipComponent) obj.objectReferenceValue;
            var fileRefStr = "Assets/" + metadataProperty.fileRef.Replace("\\", "/");

            var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
            if (script == null) {
                return;
            }
        
            var binding = AirshipScriptGUI.AirshipBehaviourField(guiContent, script, obj);
        
        
            if (binding != null && target is AirshipComponent parentBinding && binding == parentBinding) {
                EditorUtility.DisplayDialog("Invalid AirshipComponent reference", "An AirshipComponent cannot reference itself!",
                    "OK");
                return;
            }
        
            if (binding != currentObject) {
                obj.objectReferenceValue = binding;
                modified.boolValue = true;
            }
        }
        
        internal static void DrawObjectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty objType, SerializedProperty modified) {
            var currentObject = obj.objectReferenceValue;
            var t = objType.stringValue != "" ? TypeReflection.GetTypeFromString(objType.stringValue) : typeof(Object);
            var newObject = EditorGUILayout.ObjectField(guiContent, currentObject, t, true);
            
            if (newObject != currentObject) {
                obj.objectReferenceValue = newObject;
                modified.boolValue = true;
            }
        }
    }
}