using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Editor.AirshipPropertyEditor {
    internal static partial class AirshipPropertyGUI {
        internal static void RenderArrayElement(Rect rect, SerializedProperty arraySerializedProperty, SerializedProperty itemInfo, int index, AirshipComponentPropertyType elementType, SerializedProperty serializedElement, SerializedProperty arrayModified, SerializedProperty objectRefs, [CanBeNull] Type objectType, out string errorReason) {
            var label = $"Element {index}";
            errorReason = "";
            switch (elementType) {
                case AirshipComponentPropertyType.AirshipString: {
                    var arrayType = itemInfo.FindPropertyRelative("type");

                    if (arrayType.stringValue == "StringEnum") {
                        var tsEnum = AirshipEditorInfo.Enums.GetEnum(arraySerializedProperty.FindPropertyRelative("refPath").stringValue);
                        DrawCustomStringEnumDropdown(new GUIContent(label), tsEnum, serializedElement, arrayModified, rect);
                    }
                    else {
                        var strOld = serializedElement.stringValue;
                        var strNew = EditorGUI.TextField(rect, label, strOld);
                        if (strOld != strNew) {
                            serializedElement.stringValue = strNew;
                            arrayModified.boolValue = true;
                        }
                    }
                    
                    break;
                }
                case AirshipComponentPropertyType.AirshipBoolean:
                    var boolOld = serializedElement.stringValue != "0";
                    var boolNew = EditorGUI.Toggle(rect, label, boolOld);
                    if (boolOld != boolNew) {
                        serializedElement.stringValue = boolNew ? "1" : "0";
                        arrayModified.boolValue = true;
                    }
                    break;
                case AirshipComponentPropertyType.AirshipFloat:
                    float.TryParse(serializedElement.stringValue, out var floatOld);
                    var floatNew = EditorGUI.FloatField(rect, label, floatOld);
                    if (floatOld != floatNew) {
                        serializedElement.stringValue = floatNew.ToString(CultureInfo.InvariantCulture);
                        arrayModified.boolValue = true;
                    }
                    break;
                case AirshipComponentPropertyType.AirshipInt: {
                    var arrayType = itemInfo.FindPropertyRelative("type");

                    if (arrayType.stringValue == "IntEnum") {
                        var tsEnum = AirshipEditorInfo.Enums.GetEnum(arraySerializedProperty.FindPropertyRelative("refPath").stringValue);
                        DrawCustomIntEnumDropdown(new GUIContent(label), tsEnum, serializedElement, arrayModified, rect);
                    }
                    else {
                        int.TryParse(serializedElement.stringValue, out var intOld);
                        var intNew = EditorGUI.IntField(rect, label, intOld);
                        if (intOld != intNew) {
                            serializedElement.stringValue = intNew.ToString(CultureInfo.InvariantCulture);
                            arrayModified.boolValue = true;
                        }
                    }

                    break;
                }
                case AirshipComponentPropertyType.AirshipVector3:
                    var vecOld = JsonUtility.FromJson<Vector3>(serializedElement.stringValue);
                    var vecNew = EditorGUI.Vector3Field(rect, label, vecOld);
                    if (vecOld != vecNew) {
                        serializedElement.stringValue = JsonUtility.ToJson(vecNew);
                        arrayModified.boolValue = true;
                    }
                    break;
                case AirshipComponentPropertyType.AirshipComponent: {
                    var fileRef = arraySerializedProperty.FindPropertyRelative("fileRef");
                    var script = AirshipScript.GetBinaryFileFromPath("Assets/" + fileRef.stringValue);
                    var value = objectRefs.GetArrayElementAtIndex(index);
                    
                    var objOld = objectRefs.arraySize > index ? value.objectReferenceValue as AirshipComponent : null;
                    var objNew = AirshipScriptGUI.AirshipBehaviourField(rect, new GUIContent(label), script, objOld, value);
                    if (objOld != objNew) {
                        value.objectReferenceValue = objNew;
                        arrayModified.boolValue = true;
                    }
                    break;
                }
                case AirshipComponentPropertyType.AirshipObject: {
                    var objOld = objectRefs.arraySize > index ? objectRefs.GetArrayElementAtIndex(index).objectReferenceValue : null;
                    var objNew = EditorGUI.ObjectField(rect, label, objOld, objectType, true);
                    if (objOld != objNew) {
                        objectRefs.GetArrayElementAtIndex(index).objectReferenceValue = objNew;
                        arrayModified.boolValue = true;
                    }
                    break;
                }
                default:
                    errorReason = $"Type not yet supported in Airship Array ({elementType})";
                    break;
            }        
        }

    }
}