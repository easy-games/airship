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
                    property.array.RemoveElementAtEnd();
                }
            };
            
            reorderableList.onAddCallback = (list) => {
                // list.serializedProperty.InsertArrayElementAtIndex(list.serializedProperty.arraySize);
                property.array.PushElement();
            };
        }
        
        return displayInfo;
    }
    
    internal static Color k_LiveModifiedMarginDarkThemeColor = new(1f / 255f, 153f / 255f, 235f / 255f, 0.2f);
    
    /// <summary>
    /// Draw the default properties for this inspector
    /// </summary>
    private void DrawDefaultProperties() {
        // Draw each property
        foreach (var property in serializedObject.GetProperties()) {
            var shouldHideProperty = false;
            foreach (var decorator in property.decorators) {
                if (AirshipCustomEditors.TryGetDecorator(decorator, out var propertyDecorator)) {
                    propertyDecorator.arguments = decorator.parameters.ToArray();
                    propertyDecorator.property = property;
                    propertyDecorator.serializedObject = serializedObject;
                    
                    if (!propertyDecorator.ShouldDrawProperty()) {
                        shouldHideProperty = true;
                        break;
                    }
                    
                    propertyDecorator.OnBeforeInspectorGUI();
                }
            }

            if (shouldHideProperty) continue;
            AirshipEditorGUI.PropertyField(property);
        }
    }

    private void DrawScriptReference() {
        EditorGUILayout.Space(5);
        var scriptPath = script.assetPath;

        GUI.enabled = false;
        var newScript = EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);
        GUI.enabled = true;
        
        EditorGUILayout.Space(5);
    }
    
#if AIRSHIP_INTERNAL
    private void DrawInternalDebug() {
        if (Application.isPlaying) {
            var binding = (AirshipComponent)target;
            if (binding == null) return;
            
            AirshipEditorGUI.HorizontalLine();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("GameObject Id", AirshipBehaviourRootV2.GetId(binding.gameObject).ToString());
                EditorGUILayout.LabelField("Component Id", binding.GetAirshipComponentId().ToString());
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Context", binding.context.ToString());
            EditorGUILayout.EndHorizontal();
        }
    }
#endif
    
    /// <summary>
    /// Draws the default inspector
    /// </summary>
    protected void DrawDefaultInspector() {
        DrawScriptReference();
        DrawDefaultProperties();
#if AIRSHIP_INTERNAL
        DrawInternalDebug();
#endif
    }

    internal void OnEnable() {}

    internal void OnDisable() {
        this._lists.Clear();
    }

    private void OnDestroy() {
        this._foldouts.Clear();
    }

    /// <summary>
    /// Make a field for the given property
    /// </summary>
    /// <param name="property">The property</param>
    public bool PropertyField(AirshipSerializedProperty property) {
        var result = AirshipEditorGUI.PropertyField(property);
        return result;
    }
    
    /// <summary>
    /// Make a field for the named serialized property
    /// </summary>
    /// <param name="propertyName">The name of the property</param>
    public bool PropertyField(string propertyName) {
        var property = serializedObject.FindAirshipProperty(propertyName);
        return PropertyField(property);
    }

    /// <summary>
    /// Make fields for the named serialized properties
    /// </summary>
    /// <param name="propertyNames">The nanme of the properties to make fields for</param>
    public void PropertyFields(params string[] propertyNames) {
        foreach (var propertyName in propertyNames) {
            PropertyField(serializedObject.FindAirshipProperty(propertyName));
        }
    }

    /// <summary>
    /// Make a field for the given property, with a custom label
    /// </summary>
    /// <param name="propertyName">The property name</param>
    /// <param name="label">The label to apply to this property field</param>
    public bool PropertyField(GUIContent label, string propertyName) {
        var property = serializedObject.FindAirshipProperty(propertyName);
        var value =  AirshipEditorGUI.PropertyField(label, property);
        return value;
    }

    /// <summary>
    /// Make a field for the given property, with a custom label
    /// </summary>
    /// <param name="propertyName">The property name</param>
    /// <param name="label">The label to apply to this property field</param>
    public bool PropertyField(string label, string propertyName) => PropertyField(new GUIContent(label), propertyName);
    
    /// <summary>
    /// Override this to use a custom inspector for this editor
    /// </summary>
    public virtual void OnInspectorGUI() {
        EditorGUILayout.HelpBox($"Using custom inspector {GetType().Name} but OnInspectorGUI is not overloaded", MessageType.Warning);
    }
    
    public virtual void OnSceneGUI() {}
    public virtual bool HasPreviewGUI() => false;
    public virtual void OnPreviewGUI(Rect r, GUIStyle background) {}
}