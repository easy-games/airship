using System;
using System.Globalization;
using Code.Luau;
using Luau;
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
    
    private static int DoLayerMask(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        int currentValue = property.intValue;

        int nextValue;
        if (rect != null) {
            nextValue = EditorGUI.MaskField(rect.Value, label, currentValue, GameConfig.Load().gameLayers);
        } else {
            nextValue = EditorGUILayout.MaskField(label, currentValue, GameConfig.Load().gameLayers);
        }

        if (currentValue != nextValue) {
            property.intValue = nextValue;
            property.serializedModified.boolValue = true;
        }

        return nextValue;
    }

    private static AirshipComponent DoAirshipComponent(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        if (!property.isAirshipType) return null;
        
        var currentValue = (AirshipComponent)property.serializedObjectValue.objectReferenceValue;
        if (property.serializedFileRef == null) return null;
        
        var fileRefStr = "Assets/" + property.serializedFileRef.stringValue.Replace("\\", "/");
                
        var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
        if (script == null) {
            EditorGUILayout.HelpBox($"Cannot find script at path {property.serializedFileRef.stringValue}", MessageType.Error);
            return null;
        }
                
        var binding = rect.HasValue ? AirshipScriptGUI.AirshipBehaviourField(rect.Value, label, script, currentValue) : AirshipScriptGUI.AirshipBehaviourField(label, script, currentValue);
                
        // if (binding != null && target is AirshipComponent parentBinding && binding == parentBinding) {
        //     EditorUtility.DisplayDialog("Invalid AirshipComponent reference", "An AirshipComponent cannot reference itself!",
        //         "OK");
        //     return;
        // }
                
        if (binding != currentValue) {
            property.serializedObjectValue.objectReferenceValue = binding;
            property.serializedModified.boolValue = true;
        }

        return binding;
    }
}
