using System;
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Editor.AirshipPropertyEditor {
    public struct ArrayDisplayInfo {
        public ReorderableList reorderableList;
        public AirshipComponentPropertyType listType;
        public Type objType;
        public string errorReason;
    }
    
    /// <summary>
    /// Deriving from this object will allow creating a custom editor for Airship-related objects that use serialized properties
    /// </summary>
    public abstract class AirshipEditor<T> : UnityEditor.Editor where T : Object {
        /** Maps (script name, prop name) to whether a foldout is open */
        protected static Dictionary<(string, string), bool> _openPropertyFoldouts = new();

        /** Maps (game object id, prop name) to ArrayDisplayInfo object (for Array properties) */
        protected Dictionary<(int, string), ArrayDisplayInfo> _reorderableLists = new();
        
        public void OnEnable() {
            var comp = serializedObject.targetObject;
            var metadata = serializedObject.FindProperty("m_metadata");
            var metadataProperties = metadata.FindPropertyRelative("properties");
            for (var i = 0; i < metadataProperties.arraySize; i++) {
                var serializedProperty = metadataProperties.GetArrayElementAtIndex(i);
                var arrayType = serializedProperty.FindPropertyRelative("items").FindPropertyRelative("type").stringValue;
                var itemInfo = serializedProperty.FindPropertyRelative("items");
                var listPropType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(arrayType, false);
                GetOrCreateArrayDisplayInfo(comp.GetInstanceID(), serializedProperty, serializedProperty.FindPropertyRelative("name").stringValue, listPropType, itemInfo);
            }
        }
        
        /// <summary>
        /// Adds or removes elements from targetArray so it matches the size of referenceArray.
        /// </summary>
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
        
        private ArrayDisplayInfo GetOrCreateArrayDisplayInfo(int componentInstanceId, SerializedProperty arraySerializedProperty, string propName, AirshipComponentPropertyType listType, SerializedProperty itemInfo) {
            var modified = arraySerializedProperty.FindPropertyRelative("modified");
            
            Type objType = null;
            if (listType == AirshipComponentPropertyType.AirshipObject || listType == AirshipComponentPropertyType.AirshipComponent) {
                objType = TypeReflection.GetTypeFromString(itemInfo.FindPropertyRelative("objectType").stringValue);
            }
            
            if (!_reorderableLists.TryGetValue((componentInstanceId, propName), out var displayInfo) || displayInfo.listType != listType || displayInfo.objType != objType) {
                var serializedArray = itemInfo.FindPropertyRelative("serializedItems");
                var objectRefs = itemInfo.FindPropertyRelative("objectRefs");
                var newDisplayInfo = new ArrayDisplayInfo { listType = listType, objType = objType, reorderableList = new ReorderableList(serializedObject, serializedArray, true, false, true, true)};
                
                newDisplayInfo.reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;
                newDisplayInfo.reorderableList.onRemoveCallback = (ReorderableList list) => {
                    if (list.selectedIndices.Count == 1) {
                        var deletedIndex = list.selectedIndices[0];
                        list.Deselect(deletedIndex);
                        objectRefs.DeleteArrayElementAtIndex(deletedIndex);
                    }
                    
                    list.serializedProperty.DeleteArrayElementAtIndex(list.serializedProperty.arraySize - 1);
                };

                newDisplayInfo.reorderableList.onChangedCallback = (ReorderableList list) => {
                    modified.boolValue = true;
                    // Match number of elements in inspector reorderable list to serialized objectRefs. This is to reconcile objectRefs
                    MatchReferenceArraySize(objectRefs, serializedArray);
                };

                newDisplayInfo.reorderableList.onReorderCallbackWithDetails = (ReorderableList list, int oldIndex, int newIndex) => {
                    objectRefs.MoveArrayElement(oldIndex, newIndex);
                };
                
                newDisplayInfo.reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                    AirshipPropertyGUI.RenderArrayElement(rect, arraySerializedProperty, itemInfo, index, listType, serializedArray.GetArrayElementAtIndex(index), modified, objectRefs, objType, out var errReason);
                    if (errReason.Length > 0) {
                        EditorGUI.LabelField(rect, $"{errReason}");
                    }
                };
                _reorderableLists[(componentInstanceId, propName)] = newDisplayInfo;
                return newDisplayInfo;
            }
            return displayInfo;
        }

        /// <summary>
        /// Draw the specific property
        /// </summary>
        /// <param name="instanceId">The instanceId of the object the property is on</param>
        /// <param name="metadata">The associated LuauMetadata of the object</param>
        /// <param name="property">The serialized property of the object</param>
        protected void DrawProperty(int instanceId, LuauMetadata metadata, SerializedProperty property) {
            var bindingProperties = metadata.properties;
        
            var propName = property.FindPropertyRelative("name");
            // We have to find the property (they are not in the same order)
            var bindingProp = bindingProperties.Find((p) => p.name == propName.stringValue);
            var type = property.FindPropertyRelative("type");
            var objType = property.FindPropertyRelative("objectType");
            var items = property.FindPropertyRelative("items");
            var refPath = property.FindPropertyRelative("refPath");
            var decorators = property.FindPropertyRelative("decorators");
            var value = property.FindPropertyRelative("serializedValue");
            var obj = property.FindPropertyRelative("serializedObject");
            var modified = property.FindPropertyRelative("modified");
            
            var propNameDisplay = ObjectNames.NicifyVariableName(propName.stringValue);
            var decoratorDictionary = AirshipPropertyGUI.GetDecorators(bindingProp);
            var guiContent = new GUIContent(propNameDisplay, AirshipPropertyGUI.GetTooltip("", decoratorDictionary));
        
            var arrayElementType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(
                items.FindPropertyRelative("type").stringValue, 
                AirshipPropertyGUI.HasDecorator(decorators, "int")
            );
            
            // Loop over styling decorators to display them in same order they were passed in
            foreach (var dec in bindingProp.decorators)
            {
                AirshipPropertyGUI.TryDrawDecorator(dec.name, dec.parameters);
            }
            
            if (decoratorDictionary.ContainsKey("HideInInspector")) return;

            switch (type.stringValue) {
                case "number": {
                    if (AirshipPropertyGUI.HasDecorator(decorators, "int")) {
                        AirshipPropertyGUI.DrawIntProperty(guiContent, type, decoratorDictionary, value, modified);
                    }
                    else {
                        AirshipPropertyGUI.DrawFloatProperty(guiContent, type, decoratorDictionary, value, modified);
                    }

                    break;
                }
                case "string":
                    AirshipPropertyGUI.DrawStringProperty(guiContent, type, decoratorDictionary, value, modified);
                    break;
                case "boolean" or "bool":
                    AirshipPropertyGUI.DrawBooleanProperty(guiContent, type, decorators, value, modified);
                    break;
                case "Vector2":
                    AirshipPropertyGUI.DrawVector2Property(guiContent, type, decorators, value, modified);
                    break;
                case "Vector3":
                    AirshipPropertyGUI.DrawVector3Property(guiContent, type, decorators, value, modified);
                    break;
                case "Vector4":
                    AirshipPropertyGUI.DrawVector4Property(guiContent, type, decorators, value, modified);
                    break;
                case "Matrix4x4":
                    AirshipPropertyGUI.DrawMatrix4x4Property(_openPropertyFoldouts, metadata.name, propName.stringValue, guiContent, type, decorators, value, modified);
                    break;
                case "LayerMask":
                    AirshipPropertyGUI.DrawLayerMaskProperty(guiContent, type, decorators, value, modified);
                    break;
                case "AnimationCurve":
                    AirshipPropertyGUI.DrawAnimationCurveProperty(guiContent, type, decorators, value, modified);
                    break;
                case "Rect":
                    AirshipPropertyGUI.DrawRectProperty(guiContent, type, decorators, value, modified);
                    break;
                case "Color":
                    AirshipPropertyGUI.DrawColorProperty(guiContent, type, decorators, value, modified);
                    break;
                case "Quaternion":
                    AirshipPropertyGUI.DrawQuaternionProperty(guiContent, type, decorators, value, modified);
                    break;
                case "AirshipBehaviour" :
                    AirshipPropertyGUI.DrawAirshipComponentProperty(target, guiContent, metadata, bindingProp, type, decorators, obj, modified);
                    break;
                case "object":
                case "Object":
                    AirshipPropertyGUI.DrawObjectProperty(guiContent, type, decorators, obj, objType, modified);
                    break;
                case "Array":
                    DrawCustomArrayProperty(metadata.name, instanceId, propName.stringValue, guiContent, type, decorators, arrayElementType, property, modified);
                    break;
                case "StringEnum":
                    AirshipPropertyGUI.DrawStringEnumProperty(guiContent, bindingProp, value, modified);
                    break;
                case "IntEnum":
                    AirshipPropertyGUI.DrawIntEnumProperty(guiContent, bindingProp, value, modified);
                    break;
                default:
                    GUILayout.Label($"{propName.stringValue}: {type.stringValue} not yet supported");
                    break;
            }
        }

        private void DrawCustomArrayProperty(string scriptName, int componentInstanceId, string propName, GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, AirshipComponentPropertyType arrayElementType, SerializedProperty property, SerializedProperty modified) {
            if (!_openPropertyFoldouts.TryGetValue((scriptName, propName), out bool open))
            {
                open = true;
            }
            
            var foldoutStyle = EditorStyles.foldout;
            foldoutStyle.fontStyle = FontStyle.Bold;

            var newState = EditorGUILayout.Foldout(open, guiContent, foldoutStyle);
            
            if (open) {
                var itemInfo = property.FindPropertyRelative("items");
                var listInfo = GetOrCreateArrayDisplayInfo(componentInstanceId, property, propName, arrayElementType, itemInfo);
                var reorderableList = listInfo.reorderableList;
                reorderableList.DoLayoutList();

                var rect = GUILayoutUtility.GetLastRect();
                
                // Handle drag and drop for this element
                // TODO Support for drag dropping AirshipComponent list
                if (listInfo.listType is AirshipComponentPropertyType.AirshipComponent or AirshipComponentPropertyType.AirshipObject) {
                    Event currentEvent = Event.current;
                    if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform) {
                        if (rect.Contains(currentEvent.mousePosition)) {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                            if (currentEvent.type == EventType.DragPerform) {
                                DragAndDrop.AcceptDrag();



                                var typeName = itemInfo.FindPropertyRelative("objectType").stringValue;
                                Type objType = TypeReflection.GetTypeFromString(typeName);
                                
                                var objectRefs = itemInfo.FindPropertyRelative("objectRefs");
                                var serializedItems = itemInfo.FindPropertyRelative("serializedItems");
                                // Loop over all dragged items
                                foreach (var draggedObject in DragAndDrop.objectReferences) {
                                    var objRef = draggedObject;

                                    if (listInfo.listType == AirshipComponentPropertyType.AirshipObject) {
                                        // If objType is not game object we need to parse the correct component
                                        var targetNotGameObject = objType != typeof(GameObject);
                                        if (targetNotGameObject) {
                                            if (draggedObject is GameObject draggedGo) {
                                                if (typeof(Component).IsAssignableFrom(objType)) {
                                                    var comp = draggedGo.GetComponent(objType);
                                                    if (!comp) continue;

                                                    objRef = comp;
                                                }
                                            }
                                        }

                                        if (!objType.IsInstanceOfType(draggedObject)) {
                                            continue;
                                        }
                                    }
                                    else if (listInfo.listType == AirshipComponentPropertyType.AirshipComponent) {
                                        var buildInfo = AirshipBuildInfo.Instance;
                                        var scriptPath = buildInfo.GetScriptPathByTypeName(typeName);
                                        
                                        switch (draggedObject) {
                                            case AirshipComponent component when scriptPath != null && buildInfo.Inherits(component.scriptFile, scriptPath):
                                                objRef = component;
                                                break;
                                            case AirshipComponent:
                                                continue;
                                            case GameObject go: {
                                                var firstMatchingComponent = go.GetComponents<AirshipComponent>().FirstOrDefault(f => buildInfo.Inherits(f.scriptFile, scriptPath));
                                                if (firstMatchingComponent != null) {
                                                    objRef = firstMatchingComponent;
                                                }
                                                else continue;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    // Insert object to list
                                    var index = objectRefs.arraySize;
                                    // Register in actual serialized object
                                    objectRefs.InsertArrayElementAtIndex(index);
                                    objectRefs.GetArrayElementAtIndex(index).objectReferenceValue = objRef;
                                    
                                    // Add a slot in visible list
                                    MatchReferenceArraySize(serializedItems, objectRefs);
                                    modified.boolValue = true;
                                }
                            }

                            currentEvent.Use();
                        }
                    }
                }
            }
            
            // Register new foldout state
            if (newState != open)
            {
                _openPropertyFoldouts[(scriptName, propName)] = newState;
            }
        }
            
        private void DrawPropertyMetadata(Object boundObject, AirshipScriptable scriptFile, SerializedProperty metadata) {
            EditorGUILayout.Space(5);
            var metadataProperties = metadata.FindPropertyRelative("properties");
            
            var propertyList = new List<SerializedProperty>();
            var indexDictionary = new Dictionary<string, int>();
            
            if (scriptFile.m_metadata != null) {
                for (var i = 0; i < metadataProperties.arraySize; i++) {
                    var property = metadataProperties.GetArrayElementAtIndex(i);
                    propertyList.Add(property);
                    indexDictionary.Add(scriptFile.m_metadata.properties[i].name, i);
                }
            }
            
            // Sort properties by order in non-serialized object
            propertyList.Sort((p1, p2) =>
                indexDictionary[p1.FindPropertyRelative("name").stringValue] > indexDictionary[p2.FindPropertyRelative("name").stringValue] ? 1 : -1
            );
            
            foreach (var prop in propertyList) {
                DrawProperty(boundObject.GetInstanceID(), scriptFile.m_metadata, prop);   
            }
        }

        protected void DrawProperties<TScriptable>(T target, TScriptable scriptable) where TScriptable : AirshipScriptable {
            // Handle reconciliation
            if (scriptable.m_metadata != null) {
                if (ShouldReconcile(target)) {
                    ReconcileMetadata(target);
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    CheckDefaults(target);
                }
            }
            
            var metadata = serializedObject.FindProperty("m_metadata");
            var metadataName = metadata.FindPropertyRelative("name");
            if (!string.IsNullOrEmpty(metadataName.stringValue)) {
                // DrawBinaryFileMetadata(binding, metadata);
                DrawPropertyMetadata(target, scriptable, metadata);
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        protected abstract bool ShouldReconcile(T binding);
        protected virtual void ReconcileMetadata(T binding) { }
        protected virtual void CheckDefaults(T binding) { }
    }
}