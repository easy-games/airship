using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Base class to derive custom property drawers from.
/// </summary>
public abstract class AirshipEditor : ScriptableObject {
    internal AirshipSerializedObject _serializedObject;
    protected AirshipSerializedObject serializedObject => _serializedObject;

    internal Dictionary<string, ReorderableList> _lists = new();
    internal Dictionary<string, bool> _foldouts = new();

    private void MatchReferenceArraySize(SerializedProperty targetArray, SerializedProperty referenceArray) {
        int additionalElementsInRefArray = referenceArray.arraySize - targetArray.arraySize;
        for (var i = 0; i < Math.Abs(additionalElementsInRefArray); i++) {
            if (additionalElementsInRefArray > 0) {
                targetArray.InsertArrayElementAtIndex(targetArray.arraySize);
            }
            else {
                targetArray.DeleteArrayElementAtIndex(targetArray.arraySize - 1);
            }
        }
    }
    
    internal ReorderableList GetOrCreatePropertyList(AirshipSerializedProperty property) {
        var itemInfo = property.serializedItems;
        void BindReorderableListToProperty(ReorderableList reorderableList) {
            var serializedArray = itemInfo.FindPropertyRelative("serializedItems");
            var objectRefs = itemInfo.FindPropertyRelative("objectRefs");
            var modified = property.serializedModified;
            
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                var element = property.array.GetItemAtIndex(index);
                AirshipEditorGUI.PropertyField(rect, new GUIContent($"Element {index}"), element);
            };
            
            reorderableList.onChangedCallback = (ReorderableList list) => {
                modified.boolValue = true;
                // Match number of elements in inspector reorderable list to serialized objectRefs. This is to reconcile objectRefs
                MatchReferenceArraySize(objectRefs, serializedArray);
            };
            
            reorderableList.onReorderCallbackWithDetails = (ReorderableList list, int oldIndex, int newIndex) => {
                objectRefs.MoveArrayElement(oldIndex, newIndex);
            };
            
            reorderableList.onRemoveCallback = (ReorderableList list) => {
                if (list.selectedIndices.Count == 1) {
                    var deletedIndex = list.selectedIndices[0];
                    list.Deselect(deletedIndex);
                    objectRefs.DeleteArrayElementAtIndex(deletedIndex);
                }
                
                list.serializedProperty.DeleteArrayElementAtIndex(list.serializedProperty.arraySize - 1);
            };
            
            reorderableList.onAddCallback = (list) => {
                list.serializedProperty.InsertArrayElementAtIndex(list.serializedProperty.arraySize);
            };
        }
        
        if (!_lists.TryGetValue(property.name, out var list)) {
            list = new ReorderableList(
                serializedObject.serializedObject, 
                property.serializedItems.FindPropertyRelative("serializedItems"), 
                true, 
                false, 
                true, 
                true
                );
            
            BindReorderableListToProperty(list);
        }
        
        return list;
    }
    
    protected void DrawDefault() {
        foreach (var property in _serializedObject.GetProperties()) {
            AirshipEditorGUI.PropertyField(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
        }
    }

    private void OnEnable() {
        
    }

    private void OnDisable() {
        this._lists.Clear();
        this._foldouts.Clear();
    }

    public bool PropertyField(string propertyName) {
        return AirshipEditorGUI.PropertyField(serializedObject.FindAirshipProperty(propertyName));
    }
    
    public virtual void OnInspectorGUI() {
        this.DrawDefault();
    }
}