using Luau;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class AirshipGUIDrawer : GUIDrawer {}

/// <summary>
/// Base class to create a custom property drawer
/// </summary>
public abstract class AirshipPropertyDrawer : AirshipGUIDrawer {
    /// <summary>
    /// The decorator for the property - only applies to custom property drawers targeting decorators
    /// </summary>
    public LuauMetadataDecoratorElement decorator { get; internal set; }
    
    public virtual void OnGUI(Rect position, AirshipSerializedValue property, GUIContent label)
    {
        var label1 = new GUIContent(label);
        EditorGUI.LabelField(position, label1, new GUIContent("No GUI Implemented"));
    }
    
    internal virtual VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        return null;
    }
    
    public virtual float GetPropertyHeight(AirshipSerializedValue property, GUIContent label) => 18f;
}

public abstract class AirshipDecoratorDrawer : AirshipGUIDrawer {
    public virtual void OnGUI(Rect position) {}
    public virtual float GetHeight() => 18f;
    internal virtual VisualElement CreatePropertyGUI() => (VisualElement) null;
}