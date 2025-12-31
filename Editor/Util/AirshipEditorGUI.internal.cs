using System;
using System.Globalization;
using System.Linq;
using Code.Luau;
using Editor.EditorInternal;
using JetBrains.Annotations;
using Luau;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEditor.Actions;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AirshipEditorGUI {
    /// <summary>
    /// Handle property validation for the given serialized value
    /// </summary>
    private static bool DoValidateProperty(Rect? rect, AirshipSerializedValue property, AirshipSerializedType expectedType) {
        if (property == null) {
            if (rect.GetCustomRect(out var position)) {
                EditorGUI.HelpBox(position, $"Expected property with type {expectedType}, but got null. Make sure the property is defined in TypeScript as public or with @SerializeField().", MessageType.Error);
            } else {
                EditorGUILayout.HelpBox($"Expected property with type {expectedType}, but got null. Make sure the property is defined in TypeScript as public or with @SerializeField().", MessageType.Error);
            }
            
            GUIUtility.ExitGUI();
            return false;
        }
        
        if (property.type != expectedType) {
            if (rect.GetCustomRect(out var position)) {
                EditorGUI.HelpBox(position, $"{property.name}: expected type of {expectedType}, but property is of type {property.type}", MessageType.Error);
            } else {
                EditorGUILayout.HelpBox($"{property.name}: expected type of {expectedType}, but property is of type {property.type}", MessageType.Error);
            }
            
            GUIUtility.ExitGUI();
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Handle the property events such as right click on the given serialized value
    /// </summary>
    private static void DoPropertyEvents(Rect? rect, AirshipSerializedValue property) {
        Rect position;
        if (!rect.GetCustomRect(out position)) position = GUILayoutUtility.GetLastRect();

        var currentEvent = Event.current;

        switch (currentEvent.type) {
            case EventType.MouseDown: {
                if (position.Contains(currentEvent.mousePosition) && currentEvent.button == 1 && property is AirshipSerializedProperty serializedProperty) {
                    GenericMenu menu = new GenericMenu();
                    
                    var hasPrefabItems = false;
                    if (serializedProperty.prefabOverride) {
                        var test = L10n.Tr("Apply to Prefab '{0}'");
                        menu.AddItem(new GUIContent(string.Format(test, serializedProperty.prefabInstanceRoot.name)), false, () => {
                            // PrefabUtility.ApplyPropertyOverride();
                            serializedProperty.ApplyPropertyOverride(InteractionMode.UserAction);
                        });
                        
                        menu.AddItem(new GUIContent("Revert"), false, () => {
                            serializedProperty.RevertPropertyOverride(InteractionMode.UserAction);
                        });

                        // hasPrefabItems = true;
                    } else if (serializedProperty.prefab == null && serializedProperty.isModified) {
                        menu.AddItem(new GUIContent("Reset to Default"), false, () => {
                            serializedProperty.ResetToDefault();
                        });
                        
                        // hasPrefabItems = true;
                    }
  
                    // if (AirshipClipboardUtility.CanCopy(property)) {
                    //     if (hasPrefabItems) {
                    //         menu.AddSeparator("");
                    //     }
                    //     
                    //     menu.AddItem(new GUIContent("Copy"), false, () => {
                    //         AirshipClipboardUtility.CopyValue(property);
                    //     });
                    // }

                    menu.ShowAsContext();
                }

                break;
            }
        }
    }

    private static Vector2 DoVector2Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Vector2);
        
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
        
        DoPropertyEvents(rect, property);
        return newValue;
    }
    
    private static Vector3 DoVector3Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Vector3);
        
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
        
        DoPropertyEvents(rect, property);
        return newValue;
    }
    
    private static Vector4 DoVector4Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Vector4);
        
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
        
        DoPropertyEvents(rect, property);
        return newValue;
    }
    
    private static Rect DoRectField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Rect);
        
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
        
        DoPropertyEvents(rect, property);
        return newValue;
    }

    public static Matrix4x4 DoMatrix4x4Field(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Matrix4x4);

        if (!property.editor._foldouts.TryGetValue(property.name, out bool open)) {
            open = false;
        }

        var currentValue = property.matrix4x4Value;
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, label);
        var modified = false;
        if (open) {
            for (var i = 0; i < 4; i++) {
                for (var j = 0; j < 4; j++) {
                    var newValue = EditorGUILayout.FloatField($"E{i}{j}", currentValue[i, j]);
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (newValue != currentValue[i, j])
                    {
                        currentValue[i, j] = newValue;
                        modified = true;
                    }
                }
            }
        }

        property.editor._foldouts[property.name] = open;

        if (modified) {
            property.matrix4x4Value = currentValue;
            property.isModified = true;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        return default;
    }

    private static AnimationCurve DoAnimationCurveField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.AnimationCurve);

        var prevValue = property.animationCurveValue;
        AnimationCurve nextValue;
        
        if (rect.GetCustomRect(out var position)) {
            nextValue = EditorGUI.CurveField(position, label, prevValue);
        } else {
            nextValue = EditorGUILayout.CurveField(label, prevValue);
        }

        if (!nextValue.Equals(prevValue)) {
            property.animationCurveValue = nextValue;
            property.isModified = true;
        }

        DoPropertyEvents(rect, property);
        return nextValue;
    }
    
    private static Quaternion DoQuaternionField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Quaternion);
        
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
        
        DoPropertyEvents(rect, property);
        return Quaternion.Euler(newValue.x, newValue.x, newValue.z);
    }
    
    private static Color DoColorField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Color);
        
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

        DoPropertyEvents(rect, property);
        return nextValue;
    }

    private static int DoMaskField(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.FlagEnum);
        
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
        DoValidateProperty(rect, property, AirshipSerializedType.LayerMask);
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

    private static bool DefaultValidator(AirshipComponent component, AirshipSerializedValue property) {
        if (component && property is AirshipSerializedProperty serializedProperty &&
            serializedProperty.editor.target is AirshipComponent parentBinding && component == parentBinding) {
            EditorUtility.DisplayDialog("Invalid AirshipComponent reference",
                "An AirshipComponent cannot reference itself!",
                "OK");
            return false;
        }

        return true;
    }

    private static void AddLuauObjectToProperty(AirshipSerializedValue property, Object targetObject) {
#if AIRSHIPEX_CLASS_OBJECT
        var newInstance = ScriptableObject.CreateInstance<AirshipSerializableClassObject>();
        newInstance.fileRef = property.airshipType.AssetPath;
        newInstance.type = property.airshipType.Name;
                    
                    
                    
        if (AssetDatabase.IsMainAsset(targetObject) || AssetDatabase.IsSubAsset(targetObject)) {
            newInstance.name = property.name + ":" + GUID.Generate().ToString();
            AssetDatabase.AddObjectToAsset(newInstance, targetObject);
        }
                    
        property.objectReferenceValue = newInstance;
        property.serializedObject.ApplyModifiedProperties();
        property.serializedObject.serializedObject.Update();     
#endif
    }

    private static void DoAirshipSerializedClassObject(Rect? rect, GUIContent label, AirshipSerializedValue property, bool expanded = true) {
#if AIRSHIPEX_CLASS_OBJECT
        // AirshipSerializedLuauObject
        DoValidateProperty(rect, property, AirshipSerializedType.SerializedClass);

        if (property == null || property.editor == null) {
            return;
        }
        
        bool enabled;
        if (!property.editor._foldouts.TryGetValue(property.name, out enabled)) {
            property.editor._foldouts.Add(property.name, expanded);
        }
        
        if (rect.GetCustomRect(out var position)) {
            // enabled = EditorGUI.BeginFoldoutHeaderGroup(position, label, new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Normal });
            // property.editor._foldouts[property.name] = enabled;
        } else {
            var targetObject = property.serializedObject.targetObject;
            var isMainAsset = AssetDatabase.IsMainAsset(targetObject);
            var isSubAsset = AssetDatabase.IsSubAsset(targetObject);

            // if (property.objectReferenceValue == null) {
            //     AddLuauObjectToProperty(property, targetObject);
            // }
            
            if (property.objectReferenceValue != null && !isSubAsset) {
                var foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                enabled = EditorGUI.Foldout(foldoutRect, enabled, label);
                
                if (GUI.Button(new Rect(foldoutRect) { width = 20, x = foldoutRect.xMin + foldoutRect.width - 20 }, "X")) {
                    if (AssetDatabase.IsMainAsset(targetObject)) {
                        AssetDatabase.RemoveObjectFromAsset(property.objectReferenceValue);
                    }
                
                    property.objectReferenceValue = null;
                }
                
                if (enabled) {
                    if (property.objectReferenceValue is AirshipSerializableClassObject serializedLuauObject) {
                        var refType = AirshipBuildInfo.Instance.GetTypeByPathAndName(serializedLuauObject.fileRef,
                            serializedLuauObject.type);
                        if (refType == null) {
                            EditorGUILayout.HelpBox("Type non-existant", MessageType.Info);
                        } else {
                            var referenceMetadata = refType.GetMetadataForType();
                            
                            Type customEditorType = null;
                            if (serializedLuauObject.metadata != null) {
                                customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(serializedLuauObject.metadata.name);
                            }

                            if (customEditorType != null) {
                                var serializedObject = new SerializedObject(serializedLuauObject);
                                var componentEditor = AirshipCustomEditors.GetEditorForClass(serializedLuauObject, customEditorType,
                                    serializedObject);
                                
                                componentEditor.script = refType.Script;
                                componentEditor.target = serializedLuauObject;
                                componentEditor.OnInspectorGUI();
                                serializedObject.ApplyModifiedProperties();
                            } else {
                                var obj = new AirshipSerializedObject(serializedLuauObject);
                                obj.editor = property.editor;
                            
                                foreach (var prop in obj.GetProperties()) {
                                    AirshipEditorGUI.PropertyField(prop);
                                }
                                obj.ApplyModifiedProperties();
                            }
                        }
                    }
                }
                property.editor._foldouts[property.name] = enabled; 
            } else if (!isSubAsset) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(label);
                if (GUILayout.Button($"Add {ObjectNames.NicifyVariableName(property.airshipType.Name)}")) {
                    AddLuauObjectToProperty(property, targetObject);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
#endif
    }

    private static AirshipScriptableObject DoAirshipScriptableObject(Rect? rect, GUIContent label,
        AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.AirshipScriptableObject);
        if (!property.isAirshipType) return null;
        
        var currentValue = (AirshipScriptableObject)property.serializedObjectValue.objectReferenceValue;
        if (property.serializedFileRef == null) return null;
        
        var fileRefStr = "Assets/" + property.serializedFileRef.stringValue.Replace("\\", "/");
        var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
        if (script == null) {
            EditorGUILayout.HelpBox($"Cannot find script at path {property.serializedFileRef.stringValue}", MessageType.Error);
            return null;
        }
        
        AirshipScriptableObject binding;
        if (rect.HasValue) {
            binding = AirshipScriptGUI.AirshipScriptableObjectField(rect.Value, label, property.serializedObject.targetObject, script, currentValue, property.serializedObjectValue);
        } else {
            var r = EditorGUILayout.GetControlRect(false, UnityEditor.Search.ObjectField.singleLineHeight);
            binding = AirshipScriptGUI.AirshipScriptableObjectField(r, label, property.serializedObject.targetObject, script, currentValue, property.serializedObjectValue);
        }
        
        if (binding != currentValue) {
            property.serializedObjectValue.objectReferenceValue = binding;
            property.serializedModified.boolValue = true;
        }

        DoPropertyEvents(rect, property);
        return binding;
    }
    
    private static AirshipComponent DoAirshipComponent(Rect? rect, GUIContent label, AirshipSerializedValue property, AirshipComponentPropertyValidator propertyValidator = null) {
        DoValidateProperty(rect, property, AirshipSerializedType.AirshipBehaviour);
        if (!property.isAirshipType) return null;
        
        var currentValue = (AirshipComponent)property.serializedObjectValue.objectReferenceValue;
        if (property.serializedFileRef == null) return null;
        
        var fileRefStr = "Assets/" + property.serializedFileRef.stringValue.Replace("\\", "/");
                
        var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
        if (script == null) {
            EditorGUILayout.HelpBox($"Cannot find script at path {property.serializedFileRef.stringValue}", MessageType.Error);
            return null;
        }

        if (propertyValidator == null) propertyValidator = DefaultValidator;
        var binding = rect.HasValue ? AirshipScriptGUI.AirshipBehaviourField(rect.Value, label, property.serializedObject.targetObject, script, currentValue) : 
            AirshipScriptGUI.AirshipBehaviourField(label, property.serializedObject.targetObject, script, currentValue);

        if (!propertyValidator(binding, property)) {
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
        DoValidateProperty(rect, property, AirshipSerializedType.Number);
        
        if (property.type != AirshipSerializedType.Number) {
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
            var min = Convert.ToSingle(minParams[0].value, CultureInfo.InvariantCulture);
            nextValue = Math.Max(min, nextValue);
        }
        
        if (property.TryGetDecorator("Max", out var maxParams))
        {
            var max = Convert.ToSingle(maxParams[0].value, CultureInfo.InvariantCulture);
            nextValue = Math.Min(max, nextValue);
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (prevValue != nextValue) {
            property.numberValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(rect, property);
        return nextValue;
    }
    
    private static int DoIntProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Number);
        
        if (property.type != AirshipSerializedType.Number) {
            EditorGUILayout.HelpBox($"Expected number property, got {property.type}", MessageType.Warning);
            return 0;
        }
        
        var prevValue = property.intValue;
        int nextValue;

        if (property.TryGetDecorator("Range", out var rangeProps) && rangeProps.Count >= 2) {
            int min = Convert.ToInt32(rangeProps[0].value, CultureInfo.InvariantCulture);
            int max = Convert.ToInt32(rangeProps[1].value, CultureInfo.InvariantCulture);

            if (rect.GetCustomRect(out var position)) {
                nextValue = EditorGUI.IntSlider(position, label, prevValue, min, max);
            } else {
                nextValue = EditorGUILayout.IntSlider(label, prevValue, min, max);
            }
        } else {
            if (rect.GetCustomRect(out var position)) {
                nextValue = EditorGUI.IntField(position, label, prevValue);
            } else {
                nextValue = EditorGUILayout.IntField(label, prevValue);
            }
        }

        if (property.TryGetDecorator("Min", out var minParams))
        {
            var min = Convert.ToInt32(minParams[0].value, CultureInfo.InvariantCulture);
            nextValue = Math.Max(min, nextValue);
        }
        
        if (property.TryGetDecorator("Max", out var maxParams))
        {
            var max = Convert.ToInt32(maxParams[0].value, CultureInfo.InvariantCulture);
            nextValue = Math.Min(max, nextValue);
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (prevValue != nextValue) {
            property.intValue = nextValue;
            property.serializedModified.boolValue = true;
        }
        
        DoPropertyEvents(rect, property);
        return nextValue;
    }
    
    private static bool DoBooleanProperty(Rect? rect, GUIContent label, AirshipSerializedValue property) {
        DoValidateProperty(rect, property, AirshipSerializedType.Boolean);
        
        var prevValue = property.boolValue;
        bool nextValue;

        if (property.type != AirshipSerializedType.Boolean) {
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
        DoValidateProperty(rect, property, AirshipSerializedType.String);
        
        var prevValue = property.stringValue;
        string nextValue;
        
        if (property.type != AirshipSerializedType.String) {
            EditorGUILayout.HelpBox($"Expected string property, got {property.type}", MessageType.Warning);
            return null;
        }
                
        var textAreaMaxLines = 3;
        var useTextArea = false;
        var displayTextAreaHorizontal = true;
        var displayFixedHeight = false;

        if (property.TryGetDecorator("Multiline", out var multilineParams, excludeIfHasDrawer: true)) {
            if (multilineParams.Count > 0) textAreaMaxLines = int.Parse(multilineParams[0].serializedValue);
            useTextArea = true;
            displayFixedHeight = true;
        }
        if (property.TryGetDecorator("TextArea", out var _, excludeIfHasDrawer: true))
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
        DoValidateProperty(rect, property, AirshipSerializedType.Enum);
        
        var typescriptEnum = property.enumType;
        if (typescriptEnum == null) return -1;

        switch (typescriptEnum.memberType) {
            case TypeScriptEnumMemberType.Integer: {
                int prevValue = property.enumValue.IntValue;
                int nextValue;
            
                if (rect.GetCustomRect(out var position)) {
                    nextValue = EditorGUI.Popup(position, label, prevValue, typescriptEnum.keys.Select(v => new GUIContent(ObjectNames.NicifyVariableName(v))).ToArray());
                } else {
                    nextValue = EditorGUILayout.Popup(label, prevValue, typescriptEnum.keys.Select(v => new GUIContent(ObjectNames.NicifyVariableName(v))).ToArray());
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
                    nextValue = EditorGUI.Popup(position, label, prevValue, typescriptEnum.keys.Select(v => new GUIContent(ObjectNames.NicifyVariableName(v))).ToArray());
                } else {
                    nextValue = EditorGUILayout.Popup(label, prevValue, typescriptEnum.keys.Select(v => new GUIContent(ObjectNames.NicifyVariableName(v))).ToArray());
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

    internal static Color k_LiveModifiedMarginDarkThemeColor = new(1f / 255f, 153f / 255f, 235f / 255f, 0.2f);
    internal static Color k_InvalidMarginDarkThemeColor = new(1, 0f, 0);
    private static AirshipSerializedProperty currentProperty;
    private static bool prevBold;
    
    /// <summary>
    /// Marks the beginning of a serialized property
    /// 
    /// Will modify the property appearance based on state, make sure
    /// to add a matching 'EndSerializedProperty()' call
    /// </summary>
    /// <param name="property"></param>
    internal static void BeginSerializedProperty([CanBeNull] AirshipSerializedProperty property) {
        prevBold = AirshipEditorInternals.GetBoldDefaultFont();
        if (property != null && property is { prefabOverride: true }) {
            AirshipEditorInternals.SetBoldDefaultFont(true);
        }

        currentProperty = property;
    }

    /// <summary>
    /// Marks the end of the serialized property
    /// </summary>
    internal static void EndSerializedProperty() {
        var property = currentProperty;
        if (property == null) return;

        var lastRect = GUILayoutUtility.GetLastRect();
        if (property.prefabOverride) {
            var modifiedRect = lastRect;
            modifiedRect.x = 1;
            modifiedRect.width = 2;
            Graphics.DrawTexture(modifiedRect, EditorGUIUtility.whiteTexture, new Rect(), 0, 0, 0, 0, k_LiveModifiedMarginDarkThemeColor);
        }

        if (!property.valid) {
            var modifiedRect = lastRect;
            modifiedRect.x = property.prefabOverride ? 6 : 1;
            modifiedRect.width = 2;
            Graphics.DrawTexture(modifiedRect, EditorGUIUtility.whiteTexture, new Rect(), 0, 0, 0, 0, k_InvalidMarginDarkThemeColor);
        }
        
        AirshipEditorInternals.SetBoldDefaultFont(prevBold);
    }
    
    private static GUIStyle s_TabOnlyOne;
    private static GUIStyle s_TabFirst;
    private static GUIStyle s_TabMiddle;
    private static GUIStyle s_TabLast;
    private static GUIStyle s_tabsFrameBox;
    
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

    private static bool DoArrayProperty(Rect rect, GUIContent content, AirshipSerializedProperty property,
        bool expanded = false) {
        if (property == null) {
            EditorGUI.HelpBox(rect, "Property is not an array", MessageType.Error);
            return false;
        }
        if (!property.isArray) return false;
        
        bool enabled;
        if (!property.editor._foldouts.TryGetValue(property.name, out enabled)) {
            property.editor._foldouts.Add(property.name, expanded);
        }

        var headerRect = new Rect(rect) { height = EditorStyles.foldoutHeader.fixedHeight, width = rect.width - 40 };
        var sizeRect = new Rect(rect) { width = 30, height = headerRect.height, x = rect.width - 15 };
        
        rect.height -= headerRect.height;
        rect.y += headerRect.height + 5;
        
        enabled = EditorGUI.BeginFoldoutHeaderGroup(headerRect, enabled, content, new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Normal });
        property.editor._foldouts[property.name] = enabled;

        DoPropertyEvents(headerRect, property);
        
        Event currentEvent = Event.current;
        switch (currentEvent.type) {
            case EventType.DragUpdated or EventType.DragPerform
                when property.array.elementType is AirshipSerializedType.Object or AirshipSerializedType.AirshipBehaviour or AirshipSerializedType.AirshipScriptableObject: {
                var refs = DragAndDrop.objectReferences;
                
                if (headerRect.Contains(currentEvent.mousePosition)) {
                    var consume = false;

                    foreach (var draggedObject in refs) {
                        var objRef = draggedObject;
                        var elementType = property.array.elementType;
                        
                        if (elementType is AirshipSerializedType.AirshipBehaviour or AirshipSerializedType.AirshipScriptableObject) {
                            var buildInfo = AirshipBuildInfo.Instance;
                            var scriptPath = buildInfo.GetScriptPathByTypeName(property.array.elementObjectTypeString);

                            switch (draggedObject) {
                                case AirshipComponent component when elementType is AirshipSerializedType.AirshipBehaviour && scriptPath != null && buildInfo.Inherits(component.script, scriptPath):
                                    objRef = component;
                                    consume = true;
                                    break;
                                case AirshipScriptableObject scriptableObject when elementType is AirshipSerializedType.AirshipScriptableObject && scriptPath != null && buildInfo.Inherits(scriptableObject.script, scriptPath):
                                    objRef = scriptableObject;
                                    consume = true;
                                    break;
                                case AirshipScriptableObject:
                                case AirshipComponent:
                                    continue;
                                case GameObject go: {
                                    var firstMatchingComponent = go.GetComponents<AirshipComponent>()
                                        .FirstOrDefault(f => buildInfo.Inherits(f.script, scriptPath));
                                    if (firstMatchingComponent != null) {
                                        objRef = firstMatchingComponent;
                                        consume = true;
                                    }
                                    break;
                                }
                                default:
                                    objRef = null;
                                    break;
                            }

                        } else if (property.array.elementType == AirshipSerializedType.Object) {
                            var objType = property.array.elementObjectType;
                            if (objType == null) break;
                            
                            // If objType is not game object we need to parse the correct component
                            var targetNotGameObject = objType != typeof(GameObject);
                            if (targetNotGameObject && objRef is GameObject draggedGo && typeof(Component).IsAssignableFrom(objType)) {
                                var comp = draggedGo.GetComponent(objType);
                                if (!comp) {
                                    consume = false;
                                    break;
                                }
                                objRef = comp;
                                consume = true;
                            } else if (objRef.GetType().IsAssignableFrom(objType)) {
                                consume = true;
                            } else if (objRef is GameObject && objType == typeof(GameObject)) {
                                consume = true;
                            }

                            if (!objType.IsInstanceOfType(objRef)) {
                                break;
                            }
                        }
                        
                        if (objRef != null && consume && currentEvent.type == EventType.DragPerform) {
                            property.array.InsertLastElement(objRef);
                        }
                    }

                    if (consume) {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;


                        
                        currentEvent.Use();
                    }
                }
                break;
            }
        }
        
        var lastSize = property.arraySize;

        var arrayName = property.name;
        GUI.SetNextControlName(arrayName);
        var size = EditorGUI.IntField(sizeRect, lastSize,
            new GUIStyle(EditorStyles.numberField) { alignment = TextAnchor.MiddleCenter });
        var modifyArraySize = false;
        //Handle only updating array size on focus lost
        if ((Event.current.isKey && Event.current.keyCode == KeyCode.Return &&
             GUI.GetNameOfFocusedControl() == arrayName)) {
            modifyArraySize = true;
            size = focusedIntValue;
        } else if (GUI.GetNameOfFocusedControl() == arrayName && size != lastSize) {
            focusedIntValue = size;
        }
        
        if (modifyArraySize && size != lastSize && size >= 0) {
            property.array.ResizeArray(size);
        }

        if (enabled) {
            var reorderableList = property.editor.GetOrCreateArrayList(property);
            reorderableList.DoList(rect);
        }
        
        EditorGUI.EndFoldoutHeaderGroup();
        return enabled;
    }
}
