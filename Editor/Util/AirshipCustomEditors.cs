using System;
using System.Collections.Generic;
using System.Reflection;
using Luau;
using UnityEditor;
using UnityEngine;

public static class AirshipCustomEditors {
    private static Dictionary<string, Type> editorTypes = new();
    private static Dictionary<int, AirshipEditor> editors = new();
    private static Dictionary<string, Type> decoratorBehaviourMethods = new();
    
    [InitializeOnLoadMethod]
    internal static void RegisterCustomEditors() {
        var editors = TypeCache.GetTypesWithAttribute<AirshipComponentEditorAttribute>();
        foreach (var editor in editors) {
            var attr = editor.GetCustomAttributes<AirshipComponentEditorAttribute>();
            foreach (var editorAttribute in attr) {
                if (!AirshipCustomEditors.editorTypes.TryGetValue(editorAttribute.FilePath, out var _)) {
                    AirshipCustomEditors.editorTypes.Add(editorAttribute.FilePath, editor);
                }
            }
        }
        
        var modifiers = TypeCache.GetTypesWithAttribute<AirshipComponentDecoratorAttribute>();
        foreach (var method in modifiers) {
            var attr = method.GetCustomAttribute<AirshipComponentDecoratorAttribute>();
            if (!decoratorBehaviourMethods.TryGetValue(attr.DecoratorName, out var methodInfo)) {
                decoratorBehaviourMethods.Add(attr.DecoratorName, method);
            }
        }
    }

    public static bool GetDecoratorEditor(string methodName, out Type methodInfo) {
        return decoratorBehaviourMethods.TryGetValue(methodName, out methodInfo);
    }

    public static Type GetEditorForFilePath(string filePath) {
        if (editorTypes.TryGetValue(filePath, out var editorType)) {
            return editorType;
        }

        return null;
    }

    public static AirshipEditor GetEditor(AirshipComponent component, Type type, SerializedObject serializedObject) {
        if (editors.TryGetValue(component.GetInstanceID(), out var editor)) {
            editor._serializedObject = new AirshipSerializedObject();
            editor._serializedObject.UpdateObject(editor, serializedObject, component.script.m_metadata);
            return editor;
        }

        editor = (AirshipEditor) ScriptableObject.CreateInstance(type);
        editor._serializedObject ??= new AirshipSerializedObject();
        editor._serializedObject.UpdateObject(editor, serializedObject, component.script.m_metadata);
        editors.Add(component.GetInstanceID(), editor);
        return editor;
    }
}