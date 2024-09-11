using System.Collections.Generic;
using System.Globalization;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.AirshipPropertyEditor {
    internal static partial class AirshipPropertyGUI {
        internal static void DrawQuaternionProperty(GUIContent guiContent, SerializedProperty type,
            SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
            var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Quaternion>(value.stringValue);
            var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue.eulerAngles);
            if (newValue != currentValue.eulerAngles) {
                value.stringValue = JsonUtility.ToJson(Quaternion.Euler(newValue.x, newValue.y, newValue.z));
                modified.boolValue = true;
            }
        }
        
        internal static void DrawColorProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
            var currentValue = value.stringValue != "" ? JsonUtility.FromJson<Color>(value.stringValue) : default;
            var newValue = EditorGUILayout.ColorField(guiContent, currentValue);
            if (newValue != currentValue)
            {
                value.stringValue = JsonUtility.ToJson(newValue);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawVector2Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Vector2>(value.stringValue);
            var newValue = EditorGUILayout.Vector2Field(guiContent, currentValue);
            if (newValue != currentValue) {
                value.stringValue = JsonUtility.ToJson(newValue);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawVector3Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Vector3>(value.stringValue);
            var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue);
            if (newValue != currentValue) {
                value.stringValue = JsonUtility.ToJson(newValue);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawVector4Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            var currentValue = value.stringValue == "" ? new Vector4() : JsonUtility.FromJson<Vector4>(value.stringValue);
            var newValue = EditorGUILayout.Vector4Field(guiContent, currentValue);
            if (newValue != currentValue) {
                value.stringValue = JsonUtility.ToJson(newValue);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawMatrix4x4Property(Dictionary<(string, string), bool> propertyFoldouts, string scriptName, string propName, GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            // Check if matrix is already expanded
            if (!propertyFoldouts.TryGetValue((scriptName, propName), out bool open))
            {
                open = true;
            }
            var currentValue = value.stringValue == "" ? new Matrix4x4() : JsonUtility.FromJson<Matrix4x4>(value.stringValue);
            var newState = EditorGUILayout.Foldout(open, guiContent, true);
            // If newState is opened then render matrix properties
            if (newState)
            {
                for (var i = 0; i < 4; i++)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        var newValue = EditorGUILayout.FloatField($"E{i}{j}", currentValue[i, j]);
                        if (newValue != currentValue[i, j])
                        {
                            currentValue[i, j] = newValue;
                            value.stringValue = JsonUtility.ToJson(currentValue);
                            modified.boolValue = true;
                        }
                    }
                }
            }
        
            // Register new foldout state
            if (newState != open)
            {
                propertyFoldouts[(scriptName, propName)] = newState;
            }
        }

        internal static void DrawAnimationCurveProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
            var currentValue = LuauMetadataPropertySerializer.DeserializeAnimationCurve(value.stringValue);
            var newValue = EditorGUILayout.CurveField(guiContent, currentValue);
            if (!newValue.Equals(currentValue)) {
                value.stringValue = LuauMetadataPropertySerializer.SerializeAnimationCurve(newValue);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawLayerMaskProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            if (!int.TryParse(value.stringValue, out var currentValue))
            {
                currentValue = 0;
            }
            int newValue = EditorGUILayout.LayerField(guiContent, currentValue);
            if (newValue != currentValue)
            {
                value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
                modified.boolValue = true;
            }
        }
        
        internal static void DrawRectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
        {
            var currentValue = JsonUtility.FromJson<Rect>(value.stringValue);
            var newValue = EditorGUILayout.RectField(guiContent, currentValue);
            if (newValue != currentValue) {
                value.stringValue = JsonUtility.ToJson(newValue);
                modified.boolValue = true;
            }
        }
    }
}