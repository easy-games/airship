using System;
using System.Collections.Generic;
using System.Globalization;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.AirshipPropertyEditor {
    internal static partial class AirshipPropertyGUI {
        internal static void DrawBooleanProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
            var currentValue = value.stringValue == "1";
            var newValue = EditorGUILayout.Toggle(guiContent, currentValue);
            if (newValue != currentValue) {
                value.stringValue = newValue ? "1" : "0";
                modified.boolValue = true;
            }
        }
        
        internal static void DrawStringProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
            string newValue;
        
            // Flags for using text area
            var textAreaMaxLines = 3;
            var useTextArea = false;
            var displayTextAreaHorizontal = true;
            var displayFixedHeight = false;
        
            if (modifiers.TryGetValue("Multiline", out var multilineParams))
            {
                if (multilineParams.Count > 0) textAreaMaxLines = Convert.ToInt32(multilineParams[0].value);
                useTextArea = true;
                displayFixedHeight = true;
            }
            if (modifiers.ContainsKey("TextArea"))
            {
                useTextArea = true;
                displayTextAreaHorizontal = false;
                displayFixedHeight = false;
            }

            // Render flags for text area
            if (useTextArea)
            {
                if (displayTextAreaHorizontal) EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(guiContent);

                var style = EditorStyles.textArea;

                var maxHeight = style.lineHeight * textAreaMaxLines;
                if (displayFixedHeight) style.fixedHeight = maxHeight;
                newValue = EditorGUILayout.TextArea(value.stringValue, style, new []{ GUILayout.MaxHeight(maxHeight) });
                if (displayTextAreaHorizontal) EditorGUILayout.EndHorizontal();
            }
            else
            {
                newValue = EditorGUILayout.TextField(guiContent, value.stringValue);
            }
            
            if (newValue != value.stringValue) {
                value.stringValue = newValue;
                modified.boolValue = true;
            }
        }

        
        internal static void DrawFloatProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
            float.TryParse(value.stringValue, out var currentValue);
            float newValue;
            if (modifiers.TryGetValue("Range", out var rangeProps))
            {
                var min = Convert.ToSingle(rangeProps[0].value);
                var max = Convert.ToSingle(rangeProps[1].value);
                newValue = EditorGUILayout.Slider(guiContent, currentValue, min, max);
            }
            else
            {
                newValue = EditorGUILayout.FloatField(guiContent, currentValue);   
            }
        
            if (modifiers.TryGetValue("Min", out var minParams))
            {
                newValue = Math.Max(Convert.ToSingle(minParams[0].value), newValue);
            }
            if (modifiers.TryGetValue("Max", out var maxParams))
            {
                newValue = Math.Min(Convert.ToSingle(maxParams[0].value), newValue);
            }
        
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (newValue != currentValue) {
                value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
                modified.boolValue = true;
            }
        }

        
        internal static void DrawIntProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string,List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
            int.TryParse(value.stringValue, out var currentValue);
            int newValue;
            if (modifiers.TryGetValue("Range", out var rangeProps))
            {
                var min = Convert.ToSingle(rangeProps[0].value);
                var max = Convert.ToSingle(rangeProps[1].value);
                newValue = EditorGUILayout.IntSlider(guiContent, currentValue, (int) min, (int) max);
            }
            else
            {
                newValue = EditorGUILayout.IntField(guiContent, currentValue);
            }
        
            if (modifiers.TryGetValue("Min", out var minParams))
            {
                newValue = Math.Max(Convert.ToInt32(minParams[0].value), newValue);
            }
            if (modifiers.TryGetValue("Max", out var maxParams))
            {
                newValue = Math.Min(Convert.ToInt32(maxParams[0].value), newValue);
            }
        
            if (newValue != currentValue) {
                value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
                modified.boolValue = true;
            }
        }
        
        internal static bool HasDecorator(SerializedProperty modifiers, string modifier) {
            for (var i = 0; i < modifiers.arraySize; i++) {
                var element = modifiers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (element.stringValue == modifier) {
                    return true;
                }
            }
            return false;
        }
        
        internal static string GetTooltip(string comment, Dictionary<string, List<LuauMetadataDecoratorValue>> decorators)
        {
            // First try to grab tooltip from decorators 
            if (decorators.TryGetValue("Tooltip", out var tooltipProp))
            {
                var arrayIndex = tooltipProp[0];
                return arrayIndex.value.ToString();
            }
            // Fallback to using comment as tooltip
            return comment;
        }
        
        /**
         * Returns a dictionary of (Decorator Name) -> (Parameters)
         */
        internal static Dictionary<string, List<LuauMetadataDecoratorValue>> GetDecorators(LuauMetadataProperty binding)
        {
            var decorators = binding.decorators;
            Dictionary<string, List<LuauMetadataDecoratorValue>> result = new();
            for (var i = 0; i < decorators.Count; i++)
            {
                var element = decorators[i];
                var decoratorName = element.name;
                var paramsProperty = element.parameters;
                result.Add(decoratorName, paramsProperty);
            }

            return result;
        }
        
        internal static void TryDrawDecorator(string name, List<LuauMetadataDecoratorValue> parameters)
        {
            switch (name)
            {
                case "Header":
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField((string) parameters[0].value, EditorStyles.boldLabel);
                    return;
                case "Spacing":
                    if (parameters.Count == 0)
                    {
                        EditorGUILayout.Space();
                    }
                    else
                    {
                        EditorGUILayout.Space(Convert.ToSingle(parameters[0].value));
                    }
                    return;
            }
        }
    }
}