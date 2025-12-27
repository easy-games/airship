using Luau;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Base class to create a custom property drawer
/// </summary>
public abstract class AirshipPropertyDrawer : GUIDrawer {
    /// <summary>
    /// The decorator for the property - only applies to custom property drawers targeting decorators
    /// </summary>
    public LuauMetadataDecoratorElement decorator { get; internal set; }
    
    public virtual void OnGUI(Rect position, AirshipSerializedValue property, GUIContent label)
    {
        var label1 = new GUIContent(label);
        EditorGUI.LabelField(position, label1, new GUIContent("No GUI Implemented"));
    }
    
    public virtual float GetPropertyHeight(AirshipSerializedValue property, GUIContent label) => 18f;
}