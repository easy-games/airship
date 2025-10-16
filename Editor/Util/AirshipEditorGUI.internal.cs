using System;
using System.Globalization;
using System.Linq;
using Code.Luau;
using Luau;
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
}

public static partial class AirshipEditorGUI {
    private static bool DoValidateProperty(Rect? rect, AirshipSerializedValue property, AirshipSerializedValue.PropertyType expectedType) {
        if (property.type != expectedType) {
            if (rect.GetCustomRect(out var position)) {
                EditorGUI.HelpBox(position, $"Expected {expectedType} got {property.type}", MessageType.Error);
            } else {
                EditorGUILayout.HelpBox($"Expected {expectedType} got {property.type}", MessageType.Error);
            }
            
            GUIUtility.ExitGUI();
            return false;
        }
        
        return true;
    }

    private static void DoPropertyEvents(Rect? rect, AirshipSerializedValue property) {
        Rect position;
        if (!rect.GetCustomRect(out position)) position = GUILayoutUtility.GetLastRect();

        var currentEvent = Event.current;

        switch (currentEvent.type) {
            case EventType.MouseDown: {
                if (position.Contains(currentEvent.mousePosition) && currentEvent.button == 1 && property is AirshipSerializedProperty serializedProperty) {
                    GenericMenu menu = new GenericMenu();
                    
                    if (serializedProperty.isModified) {
                        
                        menu.AddItem(new GUIContent("Reset to Default"), false, () => {
                            serializedProperty.ResetToDefault();
                            serializedProperty.isModified = false;

                            if (serializedProperty.editor.target is AirshipComponent component) {
                                EditorUtility.SetDirty(component);
                            }
                        });
                    }
                    
                    menu.ShowAsContext();
                }

                break;
            }
        }
    }

    private static Vector2 DoVector2Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.vector2Value;
        Vector2 newValue;
        if (rect.GetCustomRect(out var position)) {
            newValue = EditorGUI.Vector2Field(position, label, currentValue);
        } else {
            newValue = EditorGUILayout.Vector2Field(label, currentValue);
        }


        if (currentValue != newValue) {
            property.vector2Value = newValue;
            property.isModified = true;
        }
        
        return newValue;
    }
    
    private static Vector3 DoVector3Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.vector3Value;
        Vector3 newValue;
        if (rect.GetCustomRect(out var position)) {
            newValue = EditorGUI.Vector3Field(position, label, currentValue);
        } else {
            newValue = EditorGUILayout.Vector3Field(label, currentValue);
        }


        if (currentValue != newValue) {
            property.vector3Value = newValue;
            property.isModified = true;
        }
        
        return newValue;
    }
    
    private static Vector4 DoVector4Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.vector4Value;
        Vector4 newValue;
        if (rect.GetCustomRect(out var position)) {
            newValue = EditorGUI.Vector4Field(position, label, currentValue);
        } else {
            newValue = EditorGUILayout.Vector4Field(label, currentValue);
        }


        if (currentValue != newValue) {
            property.vector4Value = newValue;
            property.isModified = true;
        }
        
        return newValue;
    }
    
    private static Rect DoRectField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.rectValue;
        Rect newValue;
        if (rect.GetCustomRect(out var position)) {
            newValue = EditorGUI.RectField(position, label, currentValue);
        } else {
            newValue = EditorGUILayout.RectField(label, currentValue);
        }


        if (currentValue != newValue) {
            property.rectValue = newValue;
            property.isModified = true;
        }
        
        return newValue;
    }

    public static Matrix4x4 DoMatrix4x4Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.matrix4x4Value;
        Matrix4x4 newValue = default;

        return newValue;
    }
    
    private static Quaternion DoQuaternionField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.quaternionValue.eulerAngles;
        Vector3 newValue;
        if (rect.GetCustomRect(out var position)) {
            newValue = EditorGUI.Vector3Field(position, label, currentValue);
        } else {
            newValue = EditorGUILayout.Vector3Field(label, currentValue);
        }


        if (currentValue != newValue) {
            property.quaternionValue = Quaternion.Euler(newValue.x, newValue.x, newValue.z);
            property.isModified = true;
        }
        
        return Quaternion.Euler(newValue.x, newValue.x, newValue.z);
    }
    
    private static Color DoColorField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var currentValue = property.colorValue;
        Color nextValue;

        if (rect != null) {
            nextValue = EditorGUI.ColorField(rect.Value, label, currentValue);
        } else {
            nextValue = EditorGUILayout.ColorField(label, currentValue);
        }

        if (currentValue != nextValue) {
            property.colorValue = nextValue;
            property.isModified = true;
        }

        return nextValue;
    }

    private static int DoMaskField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedValue.PropertyType.FlagEnum);
        
        int currentValue = property.intValue;

        int nextValue;
        if (rect != null) {
            nextValue = EditorGUI.MaskField(rect.Value, label, currentValue, property.enumType.flagNames);
        } else {
            nextValue = EditorGUILayout.MaskField(label, currentValue, property.enumType.flagNames);
        }

        if (currentValue != nextValue) {
            property.intValue = nextValue;
            property.serializedModified.boolValue = true;
        }

        DoPropertyEvents(rect, property);
        return nextValue;
    }
    
    private static int DoLayerMaskField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
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

        DoPropertyEvents(rect, property);
        return nextValue;
    }

    public delegate bool AirshipComponentPropertyValidator(AirshipComponent value, AirshipSerializedValue property);
    
    private static AirshipComponent DoAirshipComponent(Rect? rect, GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator propertyValidator = null) {
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

        if (propertyValidator != null && !propertyValidator(binding, property)) {
            return binding;
        }
                
        if (binding != currentValue) {
            property.serializedObjectValue.objectReferenceValue = binding;
            property.serializedModified.boolValue = true;
        }

        DoPropertyEvents(rect, property);
        return binding;
    }

    private static float DoNumberProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        if (property.type != AirshipSerializedValue.PropertyType.Number) {
            EditorGUILayout.HelpBox($"Expected number property, got {property.type}", MessageType.Warning);
            return 0;
        }
        
        var prevValue = property.numberValue;
        float nextValue;

        if (property.TryGetDecorator("Range", out var rangeProps) && rangeProps.Count >= 2) {
            float min = Convert.ToSingle(rangeProps[0].value, CultureInfo.InvariantCulture);
            float max = Convert.ToSingle(rangeProps[1].value, CultureInfo.InvariantCulture);

            if (rect.GetCustomRect(out var position)) {
                nextValue = EditorGUI.Slider(position, label, prevValue, min, max);
            } else {
                nextValue = EditorGUILayout.Slider(label, prevValue, min, max);
            }
        } else {
            if (rect.GetCustomRect(out var position)) {
                nextValue = EditorGUI.FloatField(position, label, prevValue);
            } else {
                nextValue = EditorGUILayout.FloatField(label, prevValue);
            }
        }

        if (property.TryGetDecorator("Min", out var minParams))
        {
            nextValue = Convert.ToSingle(minParams[0].value, CultureInfo.InvariantCulture);
        }
        
        if (property.TryGetDecorator("Max", out var maxParams))
        {
            nextValue = Convert.ToSingle(maxParams[0].value, CultureInfo.InvariantCulture);
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (prevValue != nextValue) {
            property.numberValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(rect, property);
        return nextValue;
    }

    private static bool DoBooleanProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var prevValue = property.boolValue;
        bool nextValue;

        if (property.type != AirshipSerializedValue.PropertyType.Boolean) {
            EditorGUILayout.HelpBox($"Expected string property, got {property.type}", MessageType.Warning);
            return false;
        }

        if (rect.GetCustomRect(out var position)) {
            nextValue = EditorGUI.Toggle(position, label, prevValue);
        } else {
            nextValue = EditorGUILayout.Toggle(label, prevValue);
        }

        if (prevValue != nextValue) {
            property.boolValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(rect, property);
        return nextValue;
    }

    private static string DoStringProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var prevValue = property.stringValue;
        string nextValue;
        
        if (property.type != AirshipSerializedValue.PropertyType.String) {
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
            if (rect.GetCustomRect(out var position)) {
                if (displayTextAreaHorizontal) {
                    position = EditorGUI.PrefixLabel(position, label);
                } else {
                    var headerRect = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
                    EditorGUI.PrefixLabel(headerRect, label);

                    position.height -= headerRect.height;
                    position.y += headerRect.height;
                }

                var style = EditorStyles.textArea;
                var maxHeight = style.lineHeight * textAreaMaxLines;
                if (displayFixedHeight) style.fixedHeight = maxHeight;
                nextValue = EditorGUI.TextArea(position, prevValue, style);
            } else {
                if (displayTextAreaHorizontal) EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(label);

                var style = EditorStyles.textArea;

                var maxHeight = style.lineHeight * textAreaMaxLines;
                if (displayFixedHeight) style.fixedHeight = maxHeight;
                nextValue = EditorGUILayout.TextArea(prevValue, style, new []{ GUILayout.MaxHeight(maxHeight) });
                if (displayTextAreaHorizontal) EditorGUILayout.EndHorizontal();
            }

        } else {
            if (rect.GetCustomRect(out var position)) {
                nextValue = EditorGUI.TextField(position, label, property.stringValue);
            } else {
                nextValue = EditorGUILayout.TextField(label, property.stringValue);
            }
        }

        if (prevValue != nextValue) {
            property.stringValue = nextValue;
            property.serializedModified.boolValue = true;
        }

        DoPropertyEvents(rect, property);
        return nextValue;
    }

    private static int DoEnumProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        var typescriptEnum = property.enumType;
        if (typescriptEnum == null) return -1;

        switch (typescriptEnum.memberType) {
            case TypeScriptEnumMemberType.Integer: {
                int prevValue = property.enumValue.IntValue;
                int nextValue;
            
                if (rect.GetCustomRect(out var position)) {
                    nextValue = EditorGUI.Popup(position, label, prevValue, typescriptEnum.keysNicified.Select(v => new GUIContent(v)).ToArray());
                } else {
                    nextValue = EditorGUILayout.Popup(label, prevValue, typescriptEnum.keysNicified);
                }
            
                if (prevValue != nextValue) {
                    property.enumValue = typescriptEnum.members[nextValue];
                    property.serializedModified.boolValue = true;
                }
            
                DoPropertyEvents(rect, property);
                return nextValue;
            }
            case TypeScriptEnumMemberType.String: {
                int prevValue = typescriptEnum.IndexOf(property.enumValue.StringValue);
                int nextValue;
            
                if (rect.GetCustomRect(out var position)) {
                    nextValue = EditorGUI.Popup(position, label, prevValue, typescriptEnum.keysNicified.Select(v => new GUIContent(v)).ToArray());
                } else {
                    nextValue = EditorGUILayout.Popup(label, prevValue, typescriptEnum.keysNicified);
                }
            
                if (prevValue != nextValue) {
                    property.enumValue = typescriptEnum.members[nextValue];
                    property.serializedModified.boolValue = true;
                }
            
                DoPropertyEvents(rect, property);
                return nextValue;
            }
            default:
                return -1;
        }
    }
}
