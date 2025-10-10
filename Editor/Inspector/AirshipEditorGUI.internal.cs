using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

public static partial class AirshipEditorGUI {
    private static float DoNumberField(GUIContent label, SerializedProperty value, SerializedProperty modified, float? min, float? max, (float Min, float Max)? range) {
        float.TryParse(value.stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentValue);
        float newValue;
        
        if (range.HasValue) {
            var (rangeMin, rangeMax) = range.Value;
            newValue = EditorGUILayout.Slider(label, currentValue, rangeMin, rangeMax);
        } else {
            newValue = EditorGUILayout.FloatField(label, currentValue);
        }
        
        if (min.HasValue)
        {
            newValue = Math.Max(Convert.ToSingle(min, CultureInfo.InvariantCulture), newValue);
        }
        if (max.HasValue)
        {
            newValue = Math.Min(Convert.ToSingle(max, CultureInfo.InvariantCulture), newValue);
        }
        
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }

        return newValue;
    }
}
