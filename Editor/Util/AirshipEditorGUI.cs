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

    public static string TextProperty(Rect rect, AirshipSerializedValue property) {
        if (property.type != "string") {
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
    
    public static float NumberProperty(GUIContent label, AirshipSerializedValue property) {
        if (property.type != "number") {
            EditorGUILayout.HelpBox($"Expected number property, got {property.type}", MessageType.Warning);
            return 0;
        }

        float? minValue = null;
        float? maxValue = null;
        (float, float)? range = null;
        
        if (property.TryGetDecorator("Range", out var rangeProps)) {
            float min = Convert.ToSingle(rangeProps[0].value, CultureInfo.InvariantCulture);
            float max = Convert.ToSingle(rangeProps[1].value, CultureInfo.InvariantCulture);
            range = (min, max);
        }

        if (property.TryGetDecorator("Min", out var minParams))
        {
            minValue = Convert.ToSingle(minParams[0].value, CultureInfo.InvariantCulture);
        }
        
        if (property.TryGetDecorator("Max", out var maxParams))
        {
            maxValue = Convert.ToSingle(maxParams[0].value, CultureInfo.InvariantCulture);
        }

        return DoNumberField(label, property.serializedValue, property.serializedModified, minValue, maxValue, range);
    }
    
    public static bool BooleanProperty(GUIContent label, AirshipSerializedValue property) {
        var prevValue = property.boolValue;

        if (property.type != "boolean") {
            EditorGUILayout.HelpBox($"Expected boolean property, got {property.type}", MessageType.Warning);
            return false;
        }

        bool nextValue = EditorGUILayout.Toggle(label, prevValue);
        if (prevValue != nextValue) {
            property.boolValue = nextValue;
            property.serializedModified.boolValue = true;
        }

        return nextValue;
    }

    public static string TextProperty(GUIContent label, AirshipSerializedValue property) {
        var prevValue = property.stringValue;
        string nextValue;
        
        if (property.type != "string") {
            EditorGUILayout.HelpBox($"Expected string property, got {property.type}", MessageType.Warning);
            return null;
        }
                
        var textAreaMaxLines = 3;
        var useTextArea = false;
        var displayTextAreaHorizontal = true;
        var displayFixedHeight = false;

        if (property.TryGetDecorator("Multiline", out var multilineParams)) {
            if (multilineParams.Count > 0) textAreaMaxLines = int.Parse(multilineParams[0].serializedValue);
            useTextArea = true;
            displayFixedHeight = true;
        }
        if (property.TryGetDecorator("TextArea", out var _))
        {
            useTextArea = true;
            displayTextAreaHorizontal = false;
            displayFixedHeight = false;
        }
        
        if (useTextArea) {
            if (displayTextAreaHorizontal) EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            var style = EditorStyles.textArea;

            var maxHeight = style.lineHeight * textAreaMaxLines;
            if (displayFixedHeight) style.fixedHeight = maxHeight;
            nextValue = EditorGUILayout.TextArea(prevValue, style, new []{ GUILayout.MaxHeight(maxHeight) });
            if (displayTextAreaHorizontal) EditorGUILayout.EndHorizontal();
        } else {
            nextValue = EditorGUILayout.TextField(label, property.stringValue);
        }

        if (prevValue != nextValue) {
            property.stringValue = nextValue;
            property.serializedModified.boolValue = true;
        }

        return nextValue;
    }

    public static int EnumField(GUIContent label, AirshipSerializedValue property) {
        var typescriptEnum = property.enumType;
        if (typescriptEnum == null) return -1;

        if (typescriptEnum.memberType == TypeScriptEnumMemberType.Integer) {
            int prevValue = property.enumValue.IntValue;
            int nextValue = EditorGUILayout.Popup(label, prevValue, typescriptEnum.keys);
            
            if (prevValue != nextValue) {
                property.enumValue = typescriptEnum.members[nextValue];
                property.serializedModified.boolValue = true;
            }
            
            return nextValue;
        }

        return -1;
    }

    public static UnityEngine.Object ObjectProperty(GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.objectReferenceValue;
        var nextValue = ObjectFieldLayout(label, property.objectReferenceValue, property.objectType, true);
        
        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        return nextValue;
    }
    
    public static UnityEngine.Object ObjectProperty(Rect rect, GUIContent label, AirshipSerializedValue property) {
        if (!property.isObject) return null;
        
        var currentValue = property.serializedObjectValue.objectReferenceValue;
        var nextValue = ObjectField(rect, label, currentValue, property.objectType, true);

        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        return nextValue;
    }

    public static AirshipComponent AirshipComponentProperty(GUIContent label, AirshipSerializedValue property) {
        if (!property.isAirshipType) return null;
        
        var currentValue = (AirshipComponent)property.serializedObjectValue.objectReferenceValue;
        var fileRefStr = "Assets/" + property.serializedFileRef.stringValue.Replace("\\", "/");
                
        var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
        if (script == null) {
            EditorGUILayout.HelpBox($"Cannot find script at path {property.serializedFileRef.stringValue}", MessageType.Error);
            return null;
        }
                
        var binding = AirshipScriptGUI.AirshipBehaviourField(label, script, currentValue);
                
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
            editor.DoLayoutList();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        return false;
    }

    /// <summary>
    /// Will render the given AirshipProperty
    /// </summary>
    /// <param name="label">The label to display before the property</param>
    /// <param name="value">The property to display</param>
    /// <returns></returns>
    public static bool PropertyField(GUIContent label, AirshipSerializedValue value) {
        switch (value.type) {
            case "string": {
                TextProperty(label, value);
                return false;
            }
            case "boolean": {
                return BooleanProperty(label, value);
            }
            case "number": {
                NumberProperty(label, value);
                return false;
            }
            case "IntEnum" or "StringEnum":
                EnumField(label, value);
                break;
            case "object": {
                ObjectProperty(label, value);
                break;
            }
            case "AirshipBehaviour": {
                return AirshipComponentProperty(label, value) != null;
            }
            // Arrays can only really be used with serialized property not serialized array, due to how we set this up
            case "Array" when value is AirshipSerializedProperty property: {
                ArrayProperty(label, property);
                return false;
            }
            default: {
                EditorGUILayout.HelpBox($"{value.type} is not yet supported by PropertyFieldLayout!",
                    MessageType.Warning);
                return false;
            }
        }

        return false;
    }

    public static bool PropertyField(Rect rect, GUIContent label, AirshipSerializedValue value) {
        switch (value.type) {
            case "object": {
                ObjectProperty(rect, label, value);
                break;
            }
            default: {
                EditorGUILayout.HelpBox($"{value.type} is not yet supported by PropertyField!",
                    MessageType.Warning);
                return false;
            }
        }

        return false;
    }

    public static bool PropertyField(AirshipSerializedValue property) {
        var name = ObjectNames.NicifyVariableName(property.serializedName.stringValue);
        return PropertyField(new GUIContent(name), property);

        return false;
    }
}