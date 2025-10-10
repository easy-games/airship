using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Code.Luau;
using Editor.EditorInternal;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static partial class AirshipEditorGUI {
    public static void HorizontalLine(Color color = default, int thickness = 1, int padding = 10, int margin = 0)
    {
        color = color != default ? color : Color.grey;
        Rect r = EditorGUILayout.GetControlRect(false, GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding * 0.5f;

        switch (margin)
        {
            // expand to maximum width
            case < 0:
                r.x = 0;
                r.width = EditorGUIUtility.currentViewWidth;

                break;
            case > 0:
                // shrink line width
                r.x += margin;
                r.width -= margin * 2;

                break;
        }
        
        EditorGUI.DrawRect(r, color);
    }

    
    private static GUIStyle _nonClippingObjectField;
    internal static GUIStyle nonClippingObjectField {
        get {
            if (_nonClippingObjectField == null) {
                _nonClippingObjectField = new GUIStyle(EditorStyles.objectField) {
                    imagePosition = ImagePosition.ImageLeft,
                    clipping = TextClipping.Ellipsis,
                };
            }

            return _nonClippingObjectField;
        }
    }

    public static UnityEngine.Object ObjectField(Rect rect, GUIContent label, UnityEngine.Object currentValue, System.Type type, bool
        allowSceneObjects) {
        return AirshipObjectGUIInternal.DoObjectField(rect, rect, label, "k_objectFieldHash".GetHashCode(), currentValue, null,
            type, null, allowSceneObjects, nonClippingObjectField, AirshipObjectGUIInternal.objectFieldButtonStyle);
    }

    public static UnityEngine.Object ObjectFieldLayout(GUIContent label, UnityEngine.Object currentValue, System.Type type, bool
        allowSceneObjects) {
        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
        return ObjectField(rect, label, currentValue, type, allowSceneObjects);
    }

    private const int TabButtonHeight = 22;
    
    private static GUIStyle s_TabOnlyOne;
    private static GUIStyle s_TabFirst;
    private static GUIStyle s_TabMiddle;
    private static GUIStyle s_TabLast;
    private static Rect GetTabRect(Rect rect, int tabIndex, int tabCount, out GUIStyle tabStyle) {
        if (s_TabOnlyOne == null)
        {
            s_TabOnlyOne = "Tab onlyOne";
            s_TabFirst = "Tab first";
            s_TabMiddle = "Tab middle";
            s_TabLast = "Tab last";
        }
            
            
        tabStyle = s_TabMiddle;
        if (tabCount == 1)
        {
            tabStyle = s_TabOnlyOne;
        }
        else if (tabIndex == 0)
        {
            tabStyle = s_TabFirst;
        }
        else if (tabIndex == (tabCount - 1))
        {
            tabStyle = s_TabLast;
        }
            
            
        float tabWidth = rect.width / tabCount;
        int left = Mathf.RoundToInt(tabIndex * tabWidth);
        int right = Mathf.RoundToInt((tabIndex + 1) * tabWidth);
        return new Rect(rect.x + left, rect.y, right - left,  /* kTabButtonHeight */ TabButtonHeight);
    }

    public static int BeginTabs(int selectedIndex, GUIContent[] tabs) {
        var rect = EditorGUILayout.BeginVertical(new GUIStyle("FrameBox"));
        GUILayoutUtility.GetRect(10, TabButtonHeight);
        
        var tabRects = new Rect[tabs.Length];
        var tabStyles = new GUIStyle[tabs.Length];

        for (var i = 0; i < tabs.Length; i++) {
            tabRects[i] = GetTabRect(rect, i, tabs.Length, out tabStyles[i]);
        }

        for (var i = 0; i < tabs.Length; i++) {
            if (GUI.Toggle(tabRects[i], selectedIndex == i, tabs[i], tabStyles[i])) {
                selectedIndex = i;
            }
        }
        
        return selectedIndex;
    }

    public static void EndTabs() {
        EditorGUILayout.EndVertical();
    }
    
    internal static void BeginSettingGroup(GUIContent text) {
        var indentLevel = EditorGUI.indentLevel;
        EditorGUILayout.BeginVertical(GUILayout.Height(20));
        
        EditorGUILayout.BeginHorizontal(indentLevel == 0 ? EditorStyles.toolbar : GUIStyle.none);
        Rect r = GUILayoutUtility.GetRect(text, "IN TitleText");
        r.x += 10;
        r = EditorGUI.IndentedRect(r);
        EditorGUI.indentLevel = 0;
        
        EditorGUI.LabelField(r, text, "IN TitleText");
        
        EditorGUI.indentLevel = indentLevel;
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
    }

    internal static void EndSettingGroup() {
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
        EditorGUILayout.EndVertical();
    }

    public static string StringProperty(Rect rect, AirshipSerializedValue property) {
        if (property.type != AirshipSerializedValue.PropertyType.String) {
            EditorGUILayout.HelpBox("Expected string", MessageType.Error);
            return "";
        }

        string currentValue = property.stringValue;
        string newValue = EditorGUI.TextField(rect, currentValue);

        if (newValue != currentValue) {
            property.stringValue = newValue;
        }
        
        return newValue;
    }

    public static float NumberProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoNumberProperty(rect, label, property);
    public static float NumberProperty(GUIContent label, AirshipSerializedValue property) =>
        DoNumberProperty(null, label, property);

    public static bool BooleanProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoBooleanProperty(rect, label, property);
    
    public static bool BooleanProperty(GUIContent label, AirshipSerializedValue property) =>
        DoBooleanProperty(null, label, property);

    public static string StringProperty(GUIContent label, AirshipSerializedValue property) => DoStringProperty(null, label, property);
    public static string StringProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoStringProperty(rect, label, property);

    public static int EnumProperty(GUIContent label, AirshipSerializedValue property) =>
        DoEnumProperty(null, label, property);
    public static int EnumProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoEnumProperty(rect, label, property);

    public static UnityEngine.Object ObjectProperty(GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.objectReferenceValue;
        var nextValue = ObjectFieldLayout(label, property.objectReferenceValue, property.ObjectSerializedType, true);
        
        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        return nextValue;
    }
    
    public static UnityEngine.Object ObjectProperty(Rect rect, GUIContent label, AirshipSerializedValue property) {
        if (!property.isObject) return null;
        
        var currentValue = property.serializedObjectValue.objectReferenceValue;
        var nextValue = ObjectField(rect, label, currentValue, property.ObjectSerializedType, true);

        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        return nextValue;
    }

    public static int LayerMaskProperty(Rect rect, GUIContent label, AirshipSerializedValue value) => DoLayerMaskField(rect, label, value);
    public static int LayerMaskProperty(GUIContent label, AirshipSerializedValue value) => DoLayerMaskField(null, label, value);
    public static int FlagEnumProperty(GUIContent label, AirshipSerializedValue value) => DoMaskField(null, label, value);
    public static int FlagEnumProperty(Rect rect, GUIContent label, AirshipSerializedValue value) => DoMaskField(rect, label, value);
    
    public static AirshipComponent AirshipComponentProperty(GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator validator = null) {
        return DoAirshipComponent(null, label, property, validator);
    }
    
    public static AirshipComponent AirshipComponentProperty(Rect rect, GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator validator = null) {
        return DoAirshipComponent(rect, label, property, validator);
    }
    
    public static bool ArrayProperty(GUIContent content, AirshipSerializedProperty property) {
        if (!property.isArray) return false;

        bool enabled;
        if (!property.editor._foldouts.TryGetValue(property.name, out enabled)) {
            property.editor._foldouts.Add(property.name, false);
        }
        
        enabled = EditorGUILayout.BeginFoldoutHeaderGroup(enabled, content, new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Normal });
        property.editor._foldouts[property.name] = enabled;
        
        if (enabled) {
            var editor = property.editor.GetOrCreatePropertyList(property);
            var prevElementHeight = AirshipGUI.arrayItemHeight;

            if (property.arraySize > 0) {
            switch (property.array.elementType) {
                    case AirshipSerializedValue.PropertyType.String:
                        var style = EditorStyles.textArea;
                        var textAreaMaxLines = 3;
                        var useTextArea = false;
                        var displayTextAreaHorizontal = true;
                        var displayFixedHeight = false;
                        
                        if (property.TryGetDecorator("Multiline", out var multilineParams)) {
                            if (multilineParams.Count > 0) textAreaMaxLines = int.Parse(multilineParams[0].serializedValue);
                            useTextArea = true;
                            displayFixedHeight = true;
                        }
                        
                        if (property.TryGetDecorator("TextArea", out _)) {
                            useTextArea = true;
                            displayTextAreaHorizontal = false;
                            displayFixedHeight = false;
                        }

                        if (displayFixedHeight) {
                            var maxHeight = style.lineHeight * textAreaMaxLines;
                            style.fixedHeight = maxHeight;
                        }

                        if (useTextArea) editor.elementHeight = style.lineHeight * textAreaMaxLines;
                        if (displayTextAreaHorizontal == false) editor.elementHeight += EditorGUIUtility.singleLineHeight;
                        break;
                }
            }
            
            editor.DoLayoutList();
            AirshipGUI.arrayItemHeight = prevElementHeight;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        return enabled;
    }

    /// <summary>
    /// Draws the given airship property value
    /// </summary>
    /// <param name="label">The label to display before the property</param>
    /// <param name="value">The property to display</param>
    /// <returns>True if shown</returns>
    public static bool PropertyField(GUIContent label, AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedValue.PropertyType.String: {
                StringProperty(label, value);
                return false;
            }
            case AirshipSerializedValue.PropertyType.Boolean: {
                return BooleanProperty(label, value);
            }
            case AirshipSerializedValue.PropertyType.Number: {
                NumberProperty(label, value);
                return false;
            }
            case AirshipSerializedValue.PropertyType.Enum:
                EnumProperty(label, value);
                break;
            case AirshipSerializedValue.PropertyType.FlagEnum:
                FlagEnumProperty(label, value);
                break;
            case AirshipSerializedValue.PropertyType.LayerMask: {
                LayerMaskProperty(label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.Object: {
                ObjectProperty(label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.AirshipBehaviour: {
                return AirshipComponentProperty(label, value, (component, property) => {
                    if (component && property is AirshipSerializedProperty serializedProperty &&
                        serializedProperty.editor.target is AirshipComponent parentBinding && component == parentBinding) {
                        EditorUtility.DisplayDialog("Invalid AirshipComponent reference", "An AirshipComponent cannot reference itself!",
                                 "OK");
                        return false;
                    }

                    return true;
                }) != null;
            }
            // Arrays can only really be used with serialized property not serialized array, due to how we set this up
            case AirshipSerializedValue.PropertyType.Array when value is AirshipSerializedProperty property: {
                return ArrayProperty(label, property);
            }
            default: {
                EditorGUILayout.HelpBox($"{value.typeString} is not yet supported by PropertyFieldLayout!",
                    MessageType.Warning);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Draw a property field using a preset Rect for the size/position
    /// </summary>
    public static bool PropertyField(Rect rect, GUIContent label, AirshipSerializedValue value) {
        switch (value.type) {
            case AirshipSerializedValue.PropertyType.Object: {
                ObjectProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.String: {
                StringProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.LayerMask: {
                LayerMaskProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.AirshipBehaviour: {
                return AirshipComponentProperty(rect, label, value) != null;
            }
            case AirshipSerializedValue.PropertyType.Number: {
                NumberProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.Boolean: {
                BooleanProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.Enum: {
                EnumProperty(rect, label, value);
                break;
            }
            case AirshipSerializedValue.PropertyType.FlagEnum: {
                FlagEnumProperty(rect, label, value);
                break;
            }
            default: {
                EditorGUI.HelpBox(rect, $"{value.typeString} is not yet supported by PropertyField!",
                    MessageType.Warning);
                return false;
            }
        }

        return false;
    }

    public static bool PropertyField(AirshipSerializedValue property) {
        var name = ObjectNames.NicifyVariableName(property.serializedName.stringValue);
        return PropertyField(new GUIContent(name), property);
    }
}