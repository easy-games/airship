using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Code.Luau;
using Editor.EditorInternal;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

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
    
    private static GUIStyle _nonClippingObjectFieldError;
    internal static GUIStyle nonClippingObjectFieldError {
        get {
            if (_nonClippingObjectFieldError == null) {
                _nonClippingObjectFieldError = new GUIStyle(EditorStyles.objectField) {
                    imagePosition = ImagePosition.TextOnly,
                    clipping = TextClipping.Ellipsis,
                    normal = new GUIStyleState() {
                        textColor = new Color(1, 0.2f, 0.2f),
                    },
                    hover = new GUIStyleState() {
                        textColor = new Color(1, 0.2f, 0.2f),
                    },
                    fontStyle = FontStyle.Italic
                };
            }

            return _nonClippingObjectFieldError;
        }
    }
    
    private static GUIStyle _nonClippingObjectFieldNone;
    internal static GUIStyle nonClippingObjectFieldNone {
        get {
            if (_nonClippingObjectFieldNone == null) {
                _nonClippingObjectFieldNone = new GUIStyle(EditorStyles.objectField) {
                    imagePosition = ImagePosition.TextOnly,
                    clipping = TextClipping.Ellipsis,
                    normal = new GUIStyleState() {
                        textColor = new Color(0.6f, 0.6f, 0.6f),
                    },
                    hover = new GUIStyleState() {
                        textColor = new Color(0.6f, 0.6f, 0.6f),
                    },
                    focused = new GUIStyleState() {
                        textColor = new Color(0.6f, 0.6f, 0.6f),
                    },
                };
            }

            return _nonClippingObjectFieldNone;
        }
    }

    internal static bool ValidateProperty(AirshipSerializedProperty serializedProperty, Func<AirshipSerializedProperty, bool> validate) {
        serializedProperty.valid = validate(serializedProperty);
        return serializedProperty.valid;
    }
    
    public static UnityEngine.Object ObjectField(Rect rect, GUIContent label, UnityEngine.Object currentValue, System.Type type, bool
        allowSceneObjects, bool requiresReference) {
        return AirshipObjectGUIInternal.DoObjectField(rect, rect, label, "k_objectFieldHash".GetHashCode(), currentValue, null,
            type, null, allowSceneObjects, nonClippingObjectField, AirshipObjectGUIInternal.objectFieldButtonStyle, required: requiresReference);
    }

    public static T ObjectField<T>(Rect rect, GUIContent label, T obj, bool allowSceneObjects, bool requiresReference) where T : UnityEngine.Object {
        return (T) ObjectField(rect, label, obj, typeof(T), allowSceneObjects, requiresReference);
    }

    internal enum ScriptExportType {
        Any,
        ScriptableObject,
        AirshipBehaviour,
    }
    
    internal static AirshipScript AirshipScriptField(Rect rect, GUIContent label, AirshipScript script, 
        Action<AirshipScript> onScriptSelected, ScriptExportType scriptExportType = ScriptExportType.Any, bool allowNone = true) {
        switch (scriptExportType) {
            case ScriptExportType.ScriptableObject:
                int id = GUIUtility.GetControlID("_airshipScriptableObjectScriptFieldHash".GetHashCode(), FocusType.Keyboard, rect);
                rect = EditorGUI.PrefixLabel(rect, id, label);
                
                AirshipObjectGUIInternal.DoCustomObjectField(rect, rect, id, script, null, typeof(AirshipScript), null,
                    false, nonClippingObjectField, AirshipObjectGUIInternal.objectFieldButtonStyle, (o, types) => {
                        var selection = new AirshipScriptSelectionContext(AirshipScriptType.ScriptableObject, null, script, allowNone);
                        AirshipScriptSelectorWindow.Show(selection, null, onScriptSelected);
                    }, false);
                
                return null;
            default:
                return (AirshipScript) AirshipObjectGUIInternal.DoObjectField(rect, rect, label, "k_scriptFieldHash".GetHashCode(), script,
                    null, typeof(AirshipScript), null, false, nonClippingObjectField,
                    AirshipObjectGUIInternal.objectFieldButtonStyle);
        }
    }

    private static Object ValidateScriptableObject(Object[] references, Type objtype, SerializedProperty property, AirshipObjectGUIInternal.UnityObjectFieldValidatorOptions options) {
        if (references.Length != 1) return null;
        var obj = references[0];

        if (obj is AirshipScript script && script.scriptType == AirshipScriptType.ScriptableObject) {
            return script;
        }
        
        return null;
    }


    public static UnityEngine.Object ObjectFieldLayout(GUIContent label, UnityEngine.Object currentValue, System.Type type, bool
        allowSceneObjects, bool requiresReference) {
        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
        return ObjectField(rect, label, currentValue, type, allowSceneObjects, requiresReference);
    }

    private const int TabButtonHeight = 22;
    



    private static GUIStyle headingStyle;
    private static Texture2D headerTextureBg;

    public static void Heading(GUIContent content) {
        EditorGUILayout.LabelField(content, new GUIStyle(EditorStyles.whiteBoldLabel) {
            // alignment = TextAnchor.MiddleLeft,
            // fontSize = 13,
            padding = new RectOffset(0, 0, 0, 0)
        });
    }

    public static void BeginGroup(GUIContent label) {
        HorizontalLine(new Color(70 / 255f, 70 / 255f, 70 / 255f));
        EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(5, 5, 5, 5)});
        if (label != null) AirshipEditorGUI.Heading(label);
    }

    public static void EndGroup() {
        EditorGUILayout.EndVertical();
    }
    
    internal static void SettingCategoryHeading(GUIContent label) {
        if (headerTextureBg == null) {
            headerTextureBg = new Texture2D(1, 1);
            headerTextureBg.SetPixel(0, 0, new Color(56 / 255f, 56 / 255f, 56 / 255f));
            headerTextureBg.Apply();
        }
        
        if (headingStyle == null) {
            headingStyle = new GUIStyle(EditorStyles.foldoutHeader) {
                padding = new RectOffset(5, 5, 5 , 5),
                margin = new RectOffset(0, 0, 10, 0),
                // fixedHeight = 22,
                normal = {
                    background = headerTextureBg,
                }
            };
        }
        
        EditorGUILayout.LabelField(label, headingStyle);
    }
    
    public static int BeginTabs(int selectedIndex, GUIContent[] tabs) {
        if (s_tabsFrameBox == null) {
            s_tabsFrameBox = new GUIStyle("FrameBox") { padding = new RectOffset(10, 10, 10, 10) };
        }
        
        var rect = EditorGUILayout.BeginVertical(s_tabsFrameBox);
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

    public static float NumberProperty(Rect rect, GUIContent label, AirshipSerializedValue property, bool integer = false) =>
        DoNumberProperty(rect, label, property);
    public static float NumberProperty(GUIContent label, AirshipSerializedValue property, bool integer = false) =>
        DoNumberProperty(null, label, property);
    
    public static int IntProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoIntProperty(rect, label, property);
    public static int IntProperty(GUIContent label, AirshipSerializedValue property) =>
        DoIntProperty(null, label, property);
    
    public static float NumberSliderProperty(GUIContent label, AirshipSerializedValue property, float min, float max) {
        DoValidateProperty(null, property, AirshipSerializedType.Number);
        var nextValue = EditorGUILayout.Slider(label, property.numberValue, min, max);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (nextValue != property.numberValue) {
            property.numberValue = nextValue;
            property.isModified = true;
        }
        
        DoPropertyEvents(null, property);
        return nextValue;
    }
    
    public static float NumberSliderProperty(Rect position, GUIContent label, AirshipSerializedValue property, float min, float max) {
        DoValidateProperty(null, property, AirshipSerializedType.Number);
        var nextValue = EditorGUI.Slider(position, label, property.numberValue, min, max);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (nextValue != property.numberValue) {
            property.numberValue = nextValue;
            property.isModified = true;
        }
        
        DoPropertyEvents(null, property);
        return nextValue;
    }
    
    public static int IntSliderProperty(Rect position, GUIContent label, AirshipSerializedValue property, int min, int max) {
        DoValidateProperty(null, property, AirshipSerializedType.Number);
        var nextValue = EditorGUI.IntSlider(position, label, property.intValue, min, max);
        if (nextValue != property.intValue) {
            property.intValue = nextValue;
            property.isModified = true;
        }
        
        DoPropertyEvents(null, property);
        return nextValue;
    }
    
    public static int IntSliderProperty(GUIContent label, AirshipSerializedValue property, int min, int max) {
        DoValidateProperty(null, property, AirshipSerializedType.Number);
        var nextValue = EditorGUILayout.IntSlider(label, property.intValue, min, max);
        if (nextValue != property.intValue) {
            property.intValue = nextValue;
            property.isModified = true;
        }
        
        DoPropertyEvents(null, property);
        return nextValue;
    }

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

    public static Color ColorProperty(GUIContent label, AirshipSerializedValue property) =>
        DoColorField(null, label, property);
    
    public static Color ColorProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoColorField(rect, label, property);

    public static Vector2 Vector2Property(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoVector2Field(rect, label, property);
    
    public static Vector2 Vector2Property(GUIContent label, AirshipSerializedValue property) =>
        DoVector2Field(null, label, property);
    
    public static Vector3 Vector3Property(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoVector3Field(rect, label, property);
    
    public static Vector3 Vector3Property(GUIContent label, AirshipSerializedValue property) =>
        DoVector3Field(null, label, property);
    
    public static Vector4 Vector4Property(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoVector4Field(rect, label, property);
    
    public static Vector4 Vector4Property(GUIContent label, AirshipSerializedValue property) =>
        DoVector4Field(null, label, property);
    
    public static Quaternion QuaternionProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoQuaternionField(rect, label, property);
    
    public static Quaternion QuaternionProperty(GUIContent label, AirshipSerializedValue property) =>
        DoQuaternionField(null, label, property);
    
    public static Matrix4x4 Matrix4x4Property(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoMatrix4x4Field(rect, label, property);
    
    public static Rect RectProperty(GUIContent label, AirshipSerializedValue property) =>
        DoRectField(null, label, property);
    
    public static Rect RectProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoRectField(rect, label, property);
    
    public static Matrix4x4 Matrix4x4Property(GUIContent label, AirshipSerializedValue property) =>
        DoMatrix4x4Field(null, label, property);
    
    public static AnimationCurve AnimationCurveProperty(GUIContent label, AirshipSerializedValue property) =>
        DoAnimationCurveField(null, label, property);
    
    public static AnimationCurve AnimationCurveProperty(Rect rect, GUIContent label, AirshipSerializedValue property) =>
        DoAnimationCurveField(rect, label, property);

    public static UnityEngine.Object ObjectProperty(GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.objectReferenceValue;
        var nextValue = ObjectFieldLayout(label, property.objectReferenceValue, property.objectType, true, false);
        
        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(null, property);
        return nextValue;
    }
    
    public static UnityEngine.Object ObjectProperty(Rect rect, GUIContent label, AirshipSerializedValue property) {
        if (!property.isObject) return null;
        
        var currentValue = property.serializedObjectValue.objectReferenceValue;
        var nextValue = ObjectField(rect, label, currentValue, property.objectType, true, false);

        if (currentValue != nextValue) {
            property.serializedObjectValue.objectReferenceValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(rect, property);
        return nextValue;
    }

    public static int LayerMaskProperty(Rect rect, GUIContent label, AirshipSerializedValue value) => DoLayerMaskField(rect, label, value);
    public static int LayerMaskProperty(GUIContent label, AirshipSerializedValue value) => DoLayerMaskField(null, label, value);
    public static int FlagEnumProperty(GUIContent label, AirshipSerializedValue value) => DoMaskField(null, label, value);
    public static int FlagEnumProperty(Rect rect, GUIContent label, AirshipSerializedValue value) => DoMaskField(rect, label, value);
    
    public static AirshipComponent AirshipComponentProperty(
        GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator validator = null) {
        return DoAirshipComponent(null, label, property, validator);
    }
    
    public static AirshipComponent AirshipComponentProperty(Rect rect, GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator validator = null) {
        return DoAirshipComponent(rect, label, property, validator);
    }

    public static AirshipScriptableObject AirshipScriptableObjectProperty(GUIContent label,
        AirshipSerializedValue property) {
        return DoAirshipScriptableObject(null, label, property);
    }
    
    public static AirshipScriptableObject AirshipScriptableObjectProperty(Rect rect, GUIContent label,
        AirshipSerializedValue property) {
        return DoAirshipScriptableObject(rect, label, property);
    }
    
    private static int focusedIntValue;

    internal static float GetArrayPropertyHeight(AirshipSerializedProperty property) {
        if (!property.isArray) return 0f;

        if (!property.editor._foldouts.TryGetValue(property.name, out var enabled)) {
            property.editor._foldouts.Add(property.name, false);
        }

        var size = EditorStyles.foldoutHeader.fixedHeight;
        if (!enabled) return size;
        
        var reorderableList = property.editor.GetOrCreateArrayList(property);
        size += reorderableList.reorderableList.GetHeight() + 5;
        return size;
    }

    /// <summary>
    /// Draw an array property using the Editor GUI system
    /// </summary>
    /// <param name="rect">The rect for this array properrty</param>
    /// <param name="content">The label of this array</param>
    /// <param name="property">The array property</param>
    /// <returns></returns>
    public static bool ArrayProperty(Rect rect, GUIContent content, AirshipSerializedProperty property) {
        return DoArrayProperty(rect, content, property);
    }
    
    /// <summary>
    /// Draw an array property using the Editor GUILayout system
    /// </summary>
    /// <param name="content">The label of this array</param>
    /// <param name="property">The array property</param>
    /// <returns></returns>
    public static bool ArrayProperty(GUIContent content, AirshipSerializedProperty property) {
        return DoArrayProperty(GetPropertyControlRect(property, content), content, property);
    }

    /// <summary>
    /// Draws the decorators (if applicable) and will return true if the property should render
    /// </summary>
    /// <param name="property">The property to draw the decorators for</param>
    /// <returns></returns>
    public static bool DrawDecorators(AirshipSerializedProperty property) {
        var shouldHideProperty = false;
        var decoratorStack = AirshipGUI.GetPropertyDecoratorStack(property);
        
        
        foreach (var decorator in property.decorators) {
            if (AirshipCustomEditors.TryGetDecorator(decorator, out var propertyDecorator)) {
                propertyDecorator.arguments = decorator.parameters.ToArray();
                propertyDecorator.property = property;
                propertyDecorator.serializedObject = property.serializedObject;
                    
                if (!propertyDecorator.ShouldDrawProperty()) {
                    shouldHideProperty = true;
                    break;
                }
                    
                propertyDecorator.OnBeforeInspectorGUI();
                continue;
            }
            
            if (property.TryGetDecoratorDrawer(decorator.name, out var guiDrawer)) {
                switch (guiDrawer) {
                    case AirshipPropertyDrawer propertyDrawer when !decoratorStack.Contains(propertyDrawer): {
                        var label = new GUIContent(ObjectNames.NicifyVariableName(property.name));
                        var rect = EditorGUILayout.GetControlRect(false, propertyDrawer.GetPropertyHeight(property, label));

                        decoratorStack.Push(propertyDrawer);
                        propertyDrawer.decorator = decorator;
                        propertyDrawer.OnGUI(rect, property, label);
                        propertyDrawer.decorator = null;
                        decoratorStack.Pop();

                        if (decoratorStack.Count == 0) AirshipGUI.ClearPropertyDecoratorStack(property);
                        return false;
                    }
                    case AirshipDecoratorDrawer decoratorDrawer: {
                        var rect = EditorGUILayout.GetControlRect(false, decoratorDrawer.GetHeight());
                        decoratorDrawer.OnGUI(rect);
                        break;
                    }
                }
            }
        }

        return !shouldHideProperty;
    }
    
    /// <summary>
    /// Draws the given airship property value
    /// </summary>
    /// <param name="label">The label to display before the property</param>
    /// <param name="value">The property to display</param>
    /// <param name="includeChildren"></param>
    /// <returns>True if shown</returns>
    public static bool PropertyField(GUIContent label, AirshipSerializedValue value, bool includeChildren) {
        if (value == null) {
            EditorGUILayout.LabelField(new GUIContent(label), new GUIContent("(Missing property)"), EditorStyles.objectField);
            return false;
        }
        
        switch (value.type) {
            case AirshipSerializedType.String: {
                StringProperty(label, value);
                break;
            }
            case AirshipSerializedType.Boolean: {
                return BooleanProperty(label, value);
            }
            case AirshipSerializedType.Number: {
                NumberProperty(label, value);
                break;
            }
            case AirshipSerializedType.Enum:
                EnumProperty(label, value);
                break;
            case AirshipSerializedType.FlagEnum:
                FlagEnumProperty(label, value);
                break;
            case AirshipSerializedType.LayerMask: {
                LayerMaskProperty(label, value);
                break;
            }
            case AirshipSerializedType.Object: {
                ObjectProperty(label, value);
                break;
            }
            case AirshipSerializedType.AirshipBehaviour: {
                var customPropertyDrawer = AirshipCustomEditors.GetPropertyDrawer(value);
                if (customPropertyDrawer != null) {
                    var rect = EditorGUILayout.GetControlRect(false,
                        customPropertyDrawer.GetPropertyHeight(value, label));
                    customPropertyDrawer.OnGUI(rect, value, label);
                    return true;
                }
                
                return AirshipComponentProperty(label, value) != null;
            }
            // Arrays can only really be used with serialized property not serialized array, due to how we set this up
            case AirshipSerializedType.Array when value is AirshipSerializedProperty property: {
                return ArrayProperty(label, property);
            }
            case AirshipSerializedType.Color: {
                ColorProperty(label, value);
                break;
            }
            case AirshipSerializedType.Quaternion: {
                QuaternionProperty(label, value);
                break;
            }
            case AirshipSerializedType.Vector2: {
                Vector2Property(label, value);
                break;
            }
            case AirshipSerializedType.Vector3: {
                Vector3Property(label, value);
                break;
            }
            case AirshipSerializedType.Vector4: {
                Vector4Property(label, value);
                break;
            }
            case AirshipSerializedType.Matrix4x4: {
                Matrix4x4Property(label, value);
                break;
            }
            case AirshipSerializedType.Rect: {
                RectProperty(label, value);
                break;
            }
            case AirshipSerializedType.AnimationCurve: {
                AnimationCurveProperty(label, value);
                break;
            }
            case AirshipSerializedType.SerializedClass: {
                var customPropertyDrawer = AirshipCustomEditors.GetPropertyDrawer(value);
                if (customPropertyDrawer != null) {
                    var rect = EditorGUILayout.GetControlRect(false,
                        customPropertyDrawer.GetPropertyHeight(value, label));
                    customPropertyDrawer.OnGUI(rect, value, label);
                    return true;
                }
                
                DoAirshipSerializedClassObject(null, label, value);
                break;
            }
            case AirshipSerializedType.AirshipScriptableObject: {
                var customPropertyDrawer = AirshipCustomEditors.GetPropertyDrawer(value);
                if (customPropertyDrawer != null) {
                    var rect = EditorGUILayout.GetControlRect(false,
                        customPropertyDrawer.GetPropertyHeight(value, label));
                    customPropertyDrawer.OnGUI(rect, value, label);
                    return true;
                }
                
                DoAirshipScriptableObject(null, label, value);
                break;
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
            case AirshipSerializedType.Object: {
                ObjectProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.String: {
                StringProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.LayerMask: {
                LayerMaskProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.AirshipBehaviour: {
                return AirshipComponentProperty(rect, label, value) != null;
            }
            case AirshipSerializedType.Number: {
                NumberProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.Boolean: {
                BooleanProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.Enum: {
                EnumProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.FlagEnum: {
                FlagEnumProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.Color: {
                ColorProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.Quaternion: {
                QuaternionProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.Vector2: {
                Vector2Property(rect, label, value);
                break;
            }
            case AirshipSerializedType.Vector3: {
                Vector3Property(rect, label, value);
                break;
            }
            case AirshipSerializedType.Vector4: {
                Vector4Property(rect, label, value);
                break;
            }
            case AirshipSerializedType.Matrix4x4: {
                Matrix4x4Property(rect, label, value);
                break;
            }
            case AirshipSerializedType.Rect: {
                RectProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.AnimationCurve: {
                AnimationCurveProperty(rect, label, value);
                break;
            }
            case AirshipSerializedType.AirshipScriptableObject: {
                DoAirshipScriptableObject(rect, label, value);
                break;
            }
            case AirshipSerializedType.SerializedClass: {
                break;
            }
            // Arrays can only really be used with serialized property not serialized array, due to how we set this up
            case AirshipSerializedType.Array when value is AirshipSerializedProperty property: {
                return ArrayProperty(rect, label, property);
            }
            default: {
                EditorGUI.HelpBox(rect, $"{value.typeString} is not yet supported by PropertyField!",
                    MessageType.Warning);
                return false;
            }
        }

        return false;
    }

    public static bool PropertyField(Rect rect, AirshipSerializedProperty property) {
        return PropertyField(rect, GetPropertyLabel(property), property);
    }
    
    public static bool PropertyField(GUIContent label, AirshipSerializedValue value) {
        if (value is AirshipSerializedProperty serializedProperty) {
            return PropertyField(label, serializedProperty);
        } else {
            return PropertyField(label, value, false);
        }
    }

    /// <summary>
    /// Make a field for the given Airship Serialized Property with a custom label and tooltip
    /// </summary>
    /// <param name="label">The custom label for this property</param>
    /// <param name="serializedProperty">The property</param>
    /// <returns></returns>
    public static bool PropertyField(GUIContent label, AirshipSerializedProperty serializedProperty) {
        BeginSerializedProperty(serializedProperty);
        var res = PropertyField(label, serializedProperty, false);
        EndSerializedProperty();
        return res;
    }
    
    /// <summary>
    /// Make a field for the given Airship Serialized Property
    /// </summary>
    /// <param name="property">The property to draw the field for</param>
    /// <returns></returns>
    public static bool PropertyField(AirshipSerializedProperty property) {
        if (property == null) {
            EditorGUILayout.LabelField(new GUIContent("Property"), new GUIContent("(Missing property)"), EditorStyles.objectField);
            return false;
        }

        var content = new GUIContent(ObjectNames.NicifyVariableName(property.name));
        
        string tooltip = "";
        if (property.TryGetDecorator("Tooltip", out var tooltipParams)) {
            tooltip = tooltipParams[0].value as string;
        } else if (property.propertyMetadata != null) {
            tooltip = property.propertyMetadata.Documentation;
        }

        if (!string.IsNullOrEmpty(tooltip)) content.tooltip = tooltip;
        return PropertyField(content, property);
    }

    /// <summary>
    /// Makes the property have the appropriate 
    /// </summary>
    /// <param name="property"></param>
    public static void BeginProperty(AirshipSerializedProperty property) => BeginSerializedProperty(property);
    public static void EndProperty() => EndSerializedProperty();

    /// <summary>
    /// Gets the height of the given property
    /// </summary>
    /// <param name="property">The property to get the height of</param>
    /// <param name="label">The label of this property</param>
    /// <returns></returns>
    public static float GetPropertyHeight(AirshipSerializedValue property, GUIContent label) {
        float height = 0;
        if (property == null) return EditorGUIUtility.singleLineHeight;
        
        foreach (var decorator in property.decorators) {
            var drawerGui = AirshipCustomEditors.GetDecoratorDrawer(decorator.name);
            if (drawerGui is AirshipDecoratorDrawer decoratorDrawer) height += decoratorDrawer.GetHeight();
        }
        
        var drawer = AirshipCustomEditors.GetPropertyDrawer(property);
        if (drawer != null) {
            return height + drawer.GetPropertyHeight(property, label);
        } else if (property is AirshipSerializedProperty { isArray: true } serializedProperty) {
            return height + GetArrayPropertyHeight(serializedProperty);
        }

        switch (property.type) {
            case AirshipSerializedType.String: {
                var textAreaMaxLines = 3;
                var useTextArea = false;
                var displayTextAreaHorizontal = true;
                var displayFixedHeight = false;

                if (property.TryGetDecorator("Multiline", out var multilineParams, excludeIfHasDrawer: true)) {
                    if (multilineParams.Count > 0) textAreaMaxLines = int.Parse(multilineParams[0].serializedValue);
                    useTextArea = true;
                    displayFixedHeight = true;
                }

                if (property.TryGetDecorator("TextArea", out var _, excludeIfHasDrawer: true)) {
                    useTextArea = true;
                    displayTextAreaHorizontal = false;
                    displayFixedHeight = false;
                }

                if (!useTextArea) return height + EditorGUIUtility.singleLineHeight;
                if (!displayTextAreaHorizontal) {
                    height += EditorGUIUtility.singleLineHeight;
                }

                var style = EditorStyles.textArea;
                var maxHeight = style.lineHeight * textAreaMaxLines;
                
                return height + maxHeight;
            }
            default:
                return height + GetPropertyHeight(property.type, label);
        }
    }

    /// <summary>
    /// Gets the default label for the given property
    /// </summary>
    /// <param name="property">The property to grab the label for</param>
    /// <returns>The GUIContent for the property's label</returns>
    public static GUIContent GetPropertyLabel(AirshipSerializedValue property) {
        if (property is AirshipSerializedArrayValue arrayValue) return GetArrayElementLabel(arrayValue);
        return property == null ? new  GUIContent() : new GUIContent(ObjectNames.NicifyVariableName(property.name));
    }

    /// <summary>
    /// Gets the label for the given array element
    /// </summary>
    /// <param name="element">The array element</param>
    /// <returns></returns>
    internal static GUIContent GetArrayElementLabel(AirshipSerializedArrayValue element) {
        return element == null ? new  GUIContent() : new GUIContent($"Element {element.index}");
    }

    /// <summary>
    /// Gets the Editor GUILayout rect for the given property
    /// </summary>
    /// <param name="property">The property to grab the rect for</param>
    /// <param name="label">A custom label, if applicable - otherwise will infer the default label</param>
    /// <returns></returns>
    public static Rect GetPropertyControlRect(AirshipSerializedProperty property, GUIContent label = null) {
        var propLabel = label ?? GetPropertyLabel(property);
        var height = GetPropertyHeight(property, propLabel);
        return EditorGUILayout.GetControlRect(!string.IsNullOrEmpty(propLabel.text), height);
    }
    
    private static float GetPropertyHeight(AirshipSerializedType type, GUIContent label) {
        switch (type) {
            case AirshipSerializedType.Vector2:
            case AirshipSerializedType.Vector3:
            case AirshipSerializedType.Vector4: 
            case AirshipSerializedType.Quaternion:
            case AirshipSerializedType.Number:
                return EditorGUIUtility.singleLineHeight;
            case AirshipSerializedType.Rect:
                return EditorGUIUtility.singleLineHeight * 2;
            default:
                return EditorGUIUtility.singleLineHeight;
        }
    }
}