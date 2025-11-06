using UnityEditor;
using UnityEngine;

public static class AirshipGUI {
    internal static bool GetCustomRect(this Rect? inRect, out Rect rect) {
        if (inRect.HasValue && inRect.Value != Rect.zero) {
            rect = inRect.Value;
            return true;
        }

        rect = default;
        return false;
    }

    /// <summary>
    /// The height of the array items
    /// </summary>
    public static float arrayItemHeight { get; internal set; } = EditorGUIUtility.singleLineHeight;
    
    public static bool propertyValid { get; internal set; }
}