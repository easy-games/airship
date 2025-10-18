using System;
using System.Collections.Generic;
using Code.Luau;
using Editor.EditorInternal;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Base class to derive custom property drawers from.
/// </summary>
public abstract class AirshipEditor : ScriptableObject {
    internal Dictionary<string, ArrayDisplayInfo> _lists = new();
    internal Dictionary<string, bool> _foldouts = new();

    internal class ArrayDisplayInfo {
        public ReorderableList reorderableList;
        public AirshipSerializedProperty property;

        public float elementHeight {
            get => reorderableList.elementHeight;
            set => reorderableList.elementHeight = value;
        }

        internal bool IsArrayDataMismatched(SerializedObject serializedObject, SerializedProperty serializedArray) {
            try {
                return serializedArray.propertyPath != reorderableList.serializedProperty.propertyPath;
            } catch (ObjectDisposedException exception) {
                return true;
            }
        }
        
        public void DoLayoutList() => reorderableList.DoLayoutList();
    }
    
    public AirshipSerializedObject serializedObject { get; internal set; }
    public UnityEngine.Object target { get; internal set; }
    public AirshipScript script { get; internal set; }

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
    
    internal ArrayDisplayInfo GetOrCreateArrayList(AirshipSerializedProperty property) {
        var itemInfo = property.serializedItems;

        if (!_lists.TryGetValue(property.name, out var displayInfo)) {
            var list = new ReorderableList(
                serializedObject.serializedObject, 
                property.array, 
                true, 
                false, 
                true, 
                true
            );

            displayInfo = new ArrayDisplayInfo() {
                reorderableList = list,
                property = property,
            };

            _lists.Add(property.name, displayInfo);
            BindReorderableListToProperty(list);
        }
        
        displayInfo.reorderableList.serializedProperty = property.array;
        
        void BindReorderableListToProperty(ReorderableList reorderableList) {
            var serializedArray = itemInfo.FindPropertyRelative("serializedItems");
            var objectRefs = itemInfo.FindPropertyRelative("objectRefs");
            var modified = property.serializedModified;
            
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                var element = property.array.GetElementAtIndex(index);
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
                    var selected = list.selectedIndices[0];
                    list.Deselect(selected);
                    property.array.RemoveElementAtIndex(selected);
                } else {
                    property.array.PopElement();
                }
            };
            
            reorderableList.onAddCallback = (list) => {
                // list.serializedProperty.InsertArrayElementAtIndex(list.serializedProperty.arraySize);
                property.array.PushElement();
            };
        }
        
        return displayInfo;
    }
    
    /// <summary>
    /// Draw the default properties for this inspector
    /// </summary>
    protected void DrawDefaultProperties() {
        // Draw each property
        foreach (var property in serializedObject.GetProperties()) {
            if (property.HasDecorator("HideInInspector")) continue;
            
            if (property.TryGetDecorator("Header", out var headerParams)) {
                EditorGUILayout.Space();
                var guiStyle = EditorStyles.boldLabel;
                guiStyle.richText = true;
                var title = headerParams[0].value as string;
                EditorGUILayout.LabelField(title, guiStyle);
            }

            if (property.TryGetDecorator("Spacing", out var spacingParams)) {
                if (spacingParams.Count == 0) {
                    EditorGUILayout.Space();
                }
                else {
                    EditorGUILayout.Space(Convert.ToSingle(spacingParams[0].value));
                }
            }
        
            //var prevBold = AirshipEditorInternals.GetBoldDefaultFont();
            //AirshipEditorInternals.SetBoldDefaultFont(property.isModified);
            AirshipEditorGUI.PropertyField(new GUIContent(ObjectNames.NicifyVariableName(property.name)), property);
            // AirshipEditorInternals.SetBoldDefaultFont(prevBold);
        }
    }

    internal void OnEnable() {}

    internal void OnDisable() {
        this._lists.Clear();
    }

    private void OnDestroy() {
        this._foldouts.Clear();
    }

    public bool PropertyField(string propertyName) {
        return AirshipEditorGUI.PropertyField(serializedObject.FindAirshipProperty(propertyName));
    }

    public bool PropertyField(GUIContent label, string propertyName) {
        return AirshipEditorGUI.PropertyField(label, serializedObject.FindAirshipProperty(propertyName));
    }

    public bool PropertyField(string label, string propertyName) => PropertyField(new GUIContent(label), propertyName);
    
    public virtual void OnInspectorGUI() {
        EditorGUILayout.HelpBox($"Using custom inspector {GetType().Name} but OnInspectorGUI is not overloaded", MessageType.Warning);
    }
}