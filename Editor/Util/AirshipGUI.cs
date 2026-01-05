using System.Collections.Generic;
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
    /// A stack of the decorator GUI drawers in use, will allow us to skip over already used drawers
    /// </summary>
    internal static readonly Stack<AirshipGUIDrawer> _decoratorDrawers = new Stack<AirshipGUIDrawer>();

    private static readonly Dictionary<AirshipSerializedProperty, Stack<AirshipGUIDrawer>> _decoratorPropertyDrawers =
        new();

    internal static Stack<AirshipGUIDrawer> GetPropertyDecoratorStack(AirshipSerializedProperty property) {
        if (property.editor != null) {
            return property.editor.GetPropertyDecoratorStack(property);
        }
        
        if (_decoratorPropertyDrawers.TryGetValue(property, out var stack)) return stack;
        stack = new  Stack<AirshipGUIDrawer>();
        _decoratorPropertyDrawers.Add(property, stack);

        return stack;
    }

    internal static void ClearPropertyDecoratorStack(AirshipSerializedProperty property) {
        if (property.editor != null) {
            property.editor.ClearPropertyDecoratorStack(property);
            return;
        }
        
        _decoratorPropertyDrawers.Remove(property);
    }
    
    /// <summary>
    /// The height of the array items
    /// </summary>
    public static float arrayItemHeight { get; internal set; } = EditorGUIUtility.singleLineHeight;
    
    public static bool propertyValid { get; internal set; }
}