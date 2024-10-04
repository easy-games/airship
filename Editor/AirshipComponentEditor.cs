#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using Object = System.Object;

public struct ArrayDisplayInfo {
    public ReorderableList reorderableList;
    public AirshipComponentPropertyType listType;
    public Type objType;
    public string errorReason;
}

[CustomEditor(typeof(AirshipComponent))]
public class ScriptBindingEditor : UnityEditor.Editor {
    /** Maps (script name, prop name) to whether a foldout is open */
    private static Dictionary<(string, string), bool> _openPropertyFoldouts = new();

    /** Maps (game object id, prop name) to ArrayDisplayInfo object (for Array properties) */
    private Dictionary<(int, string), ArrayDisplayInfo> _reorderableLists = new();

    public void OnEnable() {
        var comp = (Component)serializedObject.targetObject;
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

    private bool debugging = false;
    
    public override void OnInspectorGUI() {
        serializedObject.Update();

        AirshipComponent binding = (AirshipComponent)target;

        if (binding.scriptFile == null && !string.IsNullOrEmpty(binding.m_fileFullPath)) {
            // Debug.Log("Setting Script File from Path: " + binding.m_fileFullPath);
            // binding.SetScriptFromPath(binding.m_fileFullPath, LuauContext.Game);
            if (binding.scriptFile == null) {
                Debug.LogWarning($"Failed to load script asset: {binding.m_fileFullPath}");
                EditorGUILayout.HelpBox("Missing reference. This is likely from renaming a script.\n\nOld path: " + binding.m_fileFullPath.Replace("Assets/Bundles/", ""), MessageType.Warning);
            }
        }

        DrawScriptBindingProperties(binding);

        if (binding.scriptFile != null && binding.scriptFile.m_metadata != null) {
            if (ShouldReconcile(binding)) {
                binding.ReconcileMetadata();
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            CheckDefaults(binding);
        }

        if (binding.scriptFile != null) {
            var metadata = serializedObject.FindProperty("m_metadata");
            var metadataName = metadata.FindPropertyRelative("name");
            if (!string.IsNullOrEmpty(metadataName.stringValue)) {
                DrawBinaryFileMetadata(binding, metadata);
            }
        }
        
#if AIRSHIP_INTERNAL
        if (Application.isPlaying) {
            AirshipEditorGUI.HorizontalLine();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("GameObject Id", AirshipBehaviourRootV2.GetId(binding.gameObject).ToString());
                EditorGUILayout.LabelField("Component Id", binding.GetAirshipComponentId().ToString());
            }
            EditorGUILayout.EndHorizontal();
        }        
#endif
        
        serializedObject.ApplyModifiedProperties();
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
                RenderArrayElement(rect, arraySerializedProperty, itemInfo, index, listType, serializedArray.GetArrayElementAtIndex(index), modified, objectRefs, objType, out var errReason);
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

    private void CheckDefaults(AirshipComponent binding) {
        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.scriptFile.m_metadata.properties;

        var setDefault = false;

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var propName = metadataProperty.FindPropertyRelative("name").stringValue;
            var originalProperty = originalMetadataProperties.Find((p) => p.name == propName);
            
            var modified = metadataProperty.FindPropertyRelative("modified");
            if (modified.boolValue) {
                bool HaveTypesChanged() {
                    if (originalProperty.type != metadataProperty.FindPropertyRelative("type").stringValue) return true;
                    if (originalProperty.objectType != metadataProperty.FindPropertyRelative("objectType").stringValue)
                        return true;

                    var itemsProperty = metadataProperty.FindPropertyRelative("items");
                    if (originalProperty.items.type != itemsProperty.FindPropertyRelative("type").stringValue)
                        return true;
                    if (originalProperty.items.objectType != itemsProperty.FindPropertyRelative("objectType").stringValue)
                        return true;
                    return false;
                }
                
                // Verify object type hasn't changed. If it has overwrite with defaults anyway
                if (!HaveTypesChanged()) continue;
            }
            
            
            var serializedValueProperty = metadataProperty.FindPropertyRelative("serializedValue");
            if (serializedValueProperty.stringValue != originalProperty.serializedValue) {
                serializedValueProperty.stringValue = originalProperty.serializedValue;
                setDefault = true;
            }
        }

        if (setDefault) {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }

    private bool ShouldReconcile(AirshipComponent binding) {
        if (binding.m_metadata == null || binding.scriptFile.m_metadata == null) return false;

        var metadata = serializedObject.FindProperty("m_metadata");
        
        var metadataProperties = metadata.FindPropertyRelative("properties");
        var originalMetadataProperties = binding.scriptFile.m_metadata?.properties;

        if (metadataProperties.arraySize != originalMetadataProperties.Count) {
            return true;
        }

        for (var i = 0; i < metadataProperties.arraySize; i++) {
            var metadataProperty = metadataProperties.GetArrayElementAtIndex(i);
            var metadataName = metadataProperty.FindPropertyRelative("name").stringValue;
            // We could use originalMetadataProperties[i] (faster), but order might be out of sync. We don't correct
            // for out of sync order in ReconcileMetadata right now.
            var originalProperty = originalMetadataProperties.Find(property => property.name == metadataName);
            if (originalProperty == null) {
                return true;
            }

            // if (originalProperty.name != metadataName) {
            //     return true;
            // }

            if (originalProperty.type != metadataProperty.FindPropertyRelative("type").stringValue) {
                return true;
            }
            
            if (originalProperty.objectType != metadataProperty.FindPropertyRelative("objectType").stringValue) {
                return true;
            }

            var decorators = metadataProperty.FindPropertyRelative("decorators");
            if (originalProperty.decorators.Count != decorators.arraySize) {
                return true;
            }

            for (var j = 0; j < decorators.arraySize; j++)
            {
                var decorator = decorators.GetArrayElementAtIndex(j);
                var originalDecorator = originalProperty.decorators[j];
                if (originalDecorator.name != decorator.FindPropertyRelative("name").stringValue) {
                    return true;
                }

                var parameters = decorator.FindPropertyRelative("parameters");
                if (originalDecorator.parameters.Count != parameters.arraySize)
                {
                    return true;
                }

                for (var k = 0; k < originalDecorator.parameters.Count; k++)
                {
                    var originalParameter = originalDecorator.parameters[k];
                    var newParameter = parameters.GetArrayElementAtIndex(k);
                    if (originalParameter.type != newParameter.FindPropertyRelative("type").stringValue)
                    {
                        return true;
                    }
                    if (!originalParameter.serializedValue.Equals(newParameter.FindPropertyRelative("serializedValue").stringValue)) {
                        return true;
                    }
                }
            }
            
            // TODO: originalProperty.items
            if (originalProperty.items.type != metadataProperty.FindPropertyRelative("items").FindPropertyRelative("type").stringValue) {
                return true;
            }
            if (originalProperty.items.objectType != metadataProperty.FindPropertyRelative("items").FindPropertyRelative("objectType").stringValue) {
                return true;
            }
        }

        return false;
    }

    private void DrawScriptBindingProperties(AirshipComponent binding) {
        EditorGUILayout.Space(5);

        var script = binding.scriptFile;
        var scriptPath = serializedObject.FindProperty("m_fileFullPath");
        var content = new GUIContent {
            text = "Script",
            tooltip = scriptPath.stringValue,
        };

        if (binding.scriptFile != null && (binding.m_metadata != null || Application.isPlaying)) {
            GUI.enabled = false;
        }
        
        var newScript = EditorGUILayout.ObjectField(content, script, typeof(AirshipScript), true);
        if (newScript != script) {
            binding.scriptFile = (AirshipScript)newScript;
            scriptPath.stringValue = newScript == null ? "" : ((AirshipScript)newScript).assetPath;
            serializedObject.ApplyModifiedProperties();
        }

        GUI.enabled = true;
        
        if (newScript == null) {
            EditorGUILayout.Space(5);
            
            var rect = GUILayoutUtility.GetLastRect();
            var style = new GUIStyle(EditorStyles.miniButton);
            style.padding = new RectOffset(50, 50, 0, 0);
            style.fixedHeight = 25;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Airship Component", style)) {
                AirshipComponentDropdown dd = new AirshipComponentDropdown(new AdvancedDropdownState(), (binaryFile) => {
                    binding.SetScript(binaryFile, Application.isPlaying);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serializedObject.targetObject);
                });
                dd.Show(rect, 300);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space(5);
    }

    private void DrawBinaryFileMetadata(AirshipComponent binding, SerializedProperty metadata) {
        EditorGUILayout.Space(5);
        var metadataProperties = metadata.FindPropertyRelative("properties");

        var propertyList = new List<SerializedProperty>();
        var indexDictionary = new Dictionary<string, int>();

        if (binding.scriptFile.m_metadata != null) {
            for (var i = 0; i < metadataProperties.arraySize; i++) {
                var property = metadataProperties.GetArrayElementAtIndex(i);
                propertyList.Add(property);
                indexDictionary.Add(binding.scriptFile.m_metadata.properties[i].name, i);
            }
        }


        // Sort properties by order in non-serialized object
        propertyList.Sort((p1, p2) =>
            indexDictionary[p1.FindPropertyRelative("name").stringValue] > indexDictionary[p2.FindPropertyRelative("name").stringValue] ? 1 : -1
        );
        
        foreach (var prop in propertyList) {
            DrawCustomProperty(binding.GetInstanceID(), binding.scriptFile.m_metadata, prop);   
        }
    }

    // NOTE: This will probably change. Whole "decorators" structure will probably be redesigned.
    private bool HasDecorator(SerializedProperty modifiers, string modifier) {
        for (var i = 0; i < modifiers.arraySize; i++) {
            var element = modifiers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
            if (element.stringValue == modifier) {
                return true;
            }
        }
        return false;
    }

    private void DrawCustomProperty(int componentInstanceId, LuauMetadata sourceMetadata, SerializedProperty property)
    {
        
        var bindingProperties = sourceMetadata.properties;
        
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
        var decoratorDictionary = GetDecorators(bindingProp);
        var guiContent = new GUIContent(propNameDisplay, GetTooltip("", decoratorDictionary));
        
        var arrayElementType = LuauMetadataPropertySerializer.GetAirshipComponentPropertyTypeFromString(
            items.FindPropertyRelative("type").stringValue, 
            HasDecorator(decorators, "int")
        );
        
        // Loop over styling decorators to display them in same order they were passed in
        foreach (var dec in bindingProp.decorators)
        {
            TryDrawDecorator(dec.name, dec.parameters);
        }
        
        if (decoratorDictionary.ContainsKey("HideInInspector"))
        {
            return;
        }
        
        switch (type.stringValue) {
            case "number":
                if (HasDecorator(decorators, "int")) {
                    DrawCustomIntProperty(guiContent, type, decoratorDictionary, value, modified);
                } else {
                    DrawCustomFloatProperty(guiContent, type, decoratorDictionary, value, modified);
                }
                break;
            case "string":
                DrawCustomStringProperty(guiContent, type, decoratorDictionary, value, modified);
                break;
            case "StringEnum":
                DrawCustomStringEnumProperty(guiContent, bindingProp, value, modified);
                break;
            case "IntEnum":
                DrawCustomIntEnumProperty(guiContent, bindingProp, value, modified);
                break;
            case "boolean" or "bool":
                DrawCustomBoolProperty(guiContent, type, decorators, value, modified);
                break;
            case "Vector2":
                DrawCustomVector2Property(guiContent, type, decorators, value, modified);
                break;
            case "Vector3":
                DrawCustomVector3Property(guiContent, type, decorators, value, modified);
                break;
            case "Vector4":
                DrawCustomVector4Property(guiContent, type, decorators, value, modified);
                break;
            case "Matrix4x4":
                DrawCustomMatrix4x4Property(sourceMetadata.name, propName.stringValue, guiContent, type, decorators, value, modified);
                break;
            case "LayerMask":
                DrawCustomLayerMaskProperty(guiContent, type, decorators, value, modified);
                break;
            case "AnimationCurve":
                DrawCustomAnimationCurveProperty(guiContent, type, decorators, value, modified);
                break;
            case "Rect":
                DrawCustomRectProperty(guiContent, type, decorators, value, modified);
                break;
            case "object":
            case "Object":
                DrawCustomObjectProperty(guiContent, type, decorators, obj, objType, modified);
                break;
            case "Array":
                DrawCustomArrayProperty(sourceMetadata.name, componentInstanceId, propName.stringValue, guiContent, type, decorators, arrayElementType, property, modified);
                break;
            case "Color":
                DrawCustomColorProperty(guiContent, type, decorators, value, modified);
                break;
            case "Quaternion":
                DrawCustomQuaternionProperty(guiContent, type, decorators, value, modified);
                break;
            case "AirshipBehaviour" :
                DrawAirshipBehaviourReferenceProperty(guiContent, sourceMetadata, bindingProp, type, decorators, obj, modified);
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

    private void RenderArrayElement(Rect rect, SerializedProperty arraySerializedProperty, SerializedProperty itemInfo, int index, AirshipComponentPropertyType elementType, SerializedProperty serializedElement, SerializedProperty arrayModified, SerializedProperty objectRefs, [CanBeNull] Type objectType, out string errorReason) {
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

    private void DrawCustomIntProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string,List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        int.TryParse(value.stringValue, out var currentValue);
        int newValue;
        if (modifiers.TryGetValue("Range", out var rangeProps))
        {
            var min = (float) rangeProps[0].value;
            var max = (float) rangeProps[0].value;
            newValue = EditorGUILayout.IntSlider(guiContent, currentValue, (int) min, (int) max);
        }
        else
        {
            newValue = EditorGUILayout.IntField(guiContent, currentValue);
        }
        
        if (modifiers.TryGetValue("Min", out var minParams))
        {
            newValue = Math.Max(Convert.ToInt32(minParams[0].value), newValue);
        }
        if (modifiers.TryGetValue("Max", out var maxParams))
        {
            newValue = Math.Min(Convert.ToInt32(maxParams[0].value), newValue);
        }
        
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }

    private void DrawCustomFloatProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        float.TryParse(value.stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentValue);
        float newValue;
        if (modifiers.TryGetValue("Range", out var rangeProps))
        {
            var min = Convert.ToSingle(rangeProps[0].value, CultureInfo.InvariantCulture);
            var max = Convert.ToSingle(rangeProps[1].value, CultureInfo.InvariantCulture);
            newValue = EditorGUILayout.Slider(guiContent, currentValue, min, max);
        }
        else
        {
            newValue = EditorGUILayout.FloatField(guiContent, currentValue);   
        }
    
        if (modifiers.TryGetValue("Min", out var minParams))
        {
            newValue = Math.Max(Convert.ToSingle(minParams[0].value, CultureInfo.InvariantCulture), newValue);
        }
        if (modifiers.TryGetValue("Max", out var maxParams))
        {
            newValue = Math.Min(Convert.ToSingle(maxParams[0].value, CultureInfo.InvariantCulture), newValue);
        }
    
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (newValue != currentValue) {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }

    private void DrawCustomStringEnumDropdown(GUIContent content, TypeScriptEnum enumerableType,
        SerializedProperty value, SerializedProperty modified, Rect? drawRect) {
                
        if (enumerableType.members.Count == 0) {
            GUI.enabled = false;

            if (drawRect.HasValue) {
                EditorGUI.Popup(drawRect.Value, content, 0, new GUIContent[] { new GUIContent("(No Values)") });
            }
            else {
                EditorGUILayout.Popup(content, 0, new GUIContent[] { new GUIContent("(No Values)") });
            }
            
            GUI.enabled = true;
            return;
        }
        
        List<GUIContent> items = new();
        foreach (var item in enumerableType.members) {
            items.Add(new GUIContent(ObjectNames.NicifyVariableName(item.Name)));
        }
        
        int idx = enumerableType.members.FindIndex(f => f.StringValue == value.stringValue);
        if (idx == -1) {
            idx = 0;
        }
        
        idx = drawRect.HasValue ? EditorGUI.Popup(drawRect.Value, content, idx, items.ToArray()) : EditorGUILayout.Popup(content, idx, items.ToArray());
        string newValue = enumerableType.members[idx].StringValue;
        
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomIntEnumDropdown(GUIContent content, TypeScriptEnum enumerableType, SerializedProperty value, SerializedProperty modified, Rect? drawRect) {
        if (enumerableType == null) {
            return;
        }
        
        if (enumerableType.members.Count == 0) {
            GUI.enabled = false;

            if (drawRect.HasValue) {
                EditorGUI.Popup(drawRect.Value, content,0, new GUIContent[] { new GUIContent("(No Values)") });
            }
            else {
                EditorGUILayout.Popup( content,0, new GUIContent[] { new GUIContent("(No Values)") });
            }
            
            
            GUI.enabled = true;
            return;
        }
        
        List<GUIContent> items = new();
        foreach (var item in enumerableType.members) {
            items.Add(new GUIContent(ObjectNames.NicifyVariableName(item.Name) + " [" + item.IntValue + "]") );
        }
            
        int idx = 0;

        int.TryParse(value.stringValue, out int currentValue);
        
        int targetIdx = enumerableType.members.FindIndex(f => f.IntValue == currentValue);
        idx = targetIdx != -1 ? targetIdx : 0;
        
            
        idx = drawRect.HasValue ? EditorGUI.Popup(drawRect.Value, content, idx, items.ToArray()) : EditorGUILayout.Popup(content, idx, items.ToArray());
        string newValue = enumerableType.members[idx].IntValue.ToString(CultureInfo.InvariantCulture);
        
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
            modified.boolValue = true;
        }
    }

    private void DrawCustomIntEnumProperty(GUIContent guiContent, LuauMetadataProperty metadataProperty,
        SerializedProperty value, SerializedProperty modified) {
        //
        if (!AirshipEditorInfo.Instance) return;

        if (metadataProperty.refPath == null) {
            return;
        }
        
        var tsEnum = AirshipEditorInfo.Enums.GetEnum(metadataProperty.refPath);
        if (tsEnum == null) return;

        DrawCustomIntEnumDropdown(guiContent, tsEnum, value, modified, null);
    }
    
    private void DrawCustomStringEnumProperty(GUIContent guiContent, LuauMetadataProperty metadataProperty, SerializedProperty value,
        SerializedProperty modified) {
        if (!AirshipEditorInfo.Instance) return;
        
        var tsEnum = AirshipEditorInfo.Enums.GetEnum(metadataProperty.refPath);
        if (tsEnum == null) return;

        DrawCustomStringEnumDropdown(guiContent, tsEnum, value, modified, null);
    }
    
    private void DrawCustomStringProperty(GUIContent guiContent, SerializedProperty type, Dictionary<string, List<LuauMetadataDecoratorValue>> modifiers, SerializedProperty value, SerializedProperty modified) {
        string newValue;
        
        // Flags for using text area
        var textAreaMaxLines = 3;
        var useTextArea = false;
        var displayTextAreaHorizontal = true;
        var displayFixedHeight = false;
        
        if (modifiers.TryGetValue("Multiline", out var multilineParams))
        {
            if (multilineParams.Count > 0) textAreaMaxLines = Convert.ToInt32(multilineParams[0].value);
            useTextArea = true;
            displayFixedHeight = true;
        }
        if (modifiers.ContainsKey("TextArea"))
        {
            useTextArea = true;
            displayTextAreaHorizontal = false;
            displayFixedHeight = false;
        }

        // Render flags for text area
        if (useTextArea)
        {
            if (displayTextAreaHorizontal) EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(guiContent);

            var style = EditorStyles.textArea;

            var maxHeight = style.lineHeight * textAreaMaxLines;
            if (displayFixedHeight) style.fixedHeight = maxHeight;
            newValue = EditorGUILayout.TextArea(value.stringValue, style, new []{ GUILayout.MaxHeight(maxHeight) });
            if (displayTextAreaHorizontal) EditorGUILayout.EndHorizontal();
        }
        else
        {
            newValue = EditorGUILayout.TextField(guiContent, value.stringValue);
        }
            
        if (newValue != value.stringValue) {
            value.stringValue = newValue;
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomBoolProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = value.stringValue == "1";
        var newValue = EditorGUILayout.Toggle(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = newValue ? "1" : "0";
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomRectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = JsonUtility.FromJson<Rect>(value.stringValue);
        var newValue = EditorGUILayout.RectField(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomLayerMaskProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        if (!int.TryParse(value.stringValue, out var currentValue))
        {
            currentValue = 0;
        }
        int newValue = EditorGUILayout.LayerField(guiContent, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = newValue.ToString(CultureInfo.InvariantCulture);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomAnimationCurveProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = LuauMetadataPropertySerializer.DeserializeAnimationCurve(value.stringValue);
        var newValue = EditorGUILayout.CurveField(guiContent, currentValue);
        if (!newValue.Equals(currentValue)) {
            value.stringValue = LuauMetadataPropertySerializer.SerializeAnimationCurve(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomMatrix4x4Property(string scriptName, string propName, GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        // Check if matrix is already expanded
        if (!_openPropertyFoldouts.TryGetValue((scriptName, propName), out bool open))
        {
            open = true;
        }
        var currentValue = value.stringValue == "" ? new Matrix4x4() : JsonUtility.FromJson<Matrix4x4>(value.stringValue);
        var newState = EditorGUILayout.Foldout(open, guiContent, true);
        // If newState is opened then render matrix properties
        if (newState)
        {
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++)
                {
                    var newValue = EditorGUILayout.FloatField($"E{i}{j}", currentValue[i, j]);
                    if (newValue != currentValue[i, j])
                    {
                        currentValue[i, j] = newValue;
                        value.stringValue = JsonUtility.ToJson(currentValue);
                        modified.boolValue = true;
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

    private void DrawCustomVector4Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = value.stringValue == "" ? new Vector4() : JsonUtility.FromJson<Vector4>(value.stringValue);
        var newValue = EditorGUILayout.Vector4Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    private void DrawCustomVector3Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Vector3>(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomVector2Property(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified)
    {
        var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Vector2>(value.stringValue);
        var newValue = EditorGUILayout.Vector2Field(guiContent, currentValue);
        if (newValue != currentValue) {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }
    
    private void DrawCustomColorProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = value.stringValue != "" ? JsonUtility.FromJson<Color>(value.stringValue) : default;
        var newValue = EditorGUILayout.ColorField(guiContent, currentValue);
        if (newValue != currentValue)
        {
            value.stringValue = JsonUtility.ToJson(newValue);
            modified.boolValue = true;
        }
    }

    private void DrawCustomQuaternionProperty(GUIContent guiContent, SerializedProperty type,
        SerializedProperty modifiers, SerializedProperty value, SerializedProperty modified) {
        var currentValue = value.stringValue == "" ? default : JsonUtility.FromJson<Quaternion>(value.stringValue);
        var newValue = EditorGUILayout.Vector3Field(guiContent, currentValue.eulerAngles);
        if (newValue != currentValue.eulerAngles) {
            value.stringValue = JsonUtility.ToJson(Quaternion.Euler(newValue.x, newValue.y, newValue.z));
            modified.boolValue = true;
        }
    }

    
    private void DrawAirshipBehaviourReferenceProperty(GUIContent guiContent, LuauMetadata metadata, LuauMetadataProperty metadataProperty, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty modified) {
        var currentObject = (AirshipComponent) obj.objectReferenceValue;
        var fileRefStr = "Assets/" + metadataProperty.fileRef.Replace("\\", "/");

        var script = AirshipScript.GetBinaryFileFromPath(fileRefStr);
        if (script == null) {
            return;
        }
        
        var binding = AirshipScriptGUI.AirshipBehaviourField(guiContent, script, obj);
        
        
        if (binding != null && target is AirshipComponent parentBinding && binding == parentBinding) {
            EditorUtility.DisplayDialog("Invalid AirshipComponent reference", "An AirshipComponent cannot reference itself!",
                "OK");
            return;
        }
        
        if (binding != currentObject) {
            obj.objectReferenceValue = binding;
            modified.boolValue = true;
        }
    }

    private void DrawCustomObjectProperty(GUIContent guiContent, SerializedProperty type, SerializedProperty modifiers, SerializedProperty obj, SerializedProperty objType, SerializedProperty modified) {
        var currentObject = obj.objectReferenceValue;
        var t = objType.stringValue != "" ? TypeReflection.GetTypeFromString(objType.stringValue) : typeof(Object);
        var newObject = EditorGUILayout.ObjectField(guiContent, currentObject, t, true);
            
        if (newObject != currentObject) {
            obj.objectReferenceValue = newObject;
            modified.boolValue = true;
        }
    }
    
    // private Color ColorFromString(string value) {
    //     var values = value.Split(",");
    //     if (values.Length != 4) {
    //         return Color.white;
    //     }
    //     
    //     float.TryParse(values[0], out var r);
    //     float.TryParse(values[1], out var g);
    //     float.TryParse(values[2], out var b);
    //     float.TryParse(values[3], out var a);
    //
    //     return new Color(r, g, b, a);
    // }
    //
    // private string ColorToString(Color value) {
    //     var r = value.r.ToString(CultureInfo.InvariantCulture);
    //     var g = value.g.ToString(CultureInfo.InvariantCulture);
    //     var b = value.b.ToString(CultureInfo.InvariantCulture);
    //     var a = value.a.ToString(CultureInfo.InvariantCulture);
    //     return $"{r},{g},{b},{a}";
    // }

    private string StripAssetsFolder(string filePath) {
        int resourcesIndex = string.IsNullOrEmpty(filePath) ? -1 : filePath.IndexOf("/Bundles/");
        if (resourcesIndex >= 0) {
            filePath = filePath.Substring(resourcesIndex + "/Bundles/".Length);
        }
        return filePath;
    }

    /**
     * Returns a dictionary of (Decorator Name) -> (Parameters)
     */
    private Dictionary<string, List<LuauMetadataDecoratorValue>> GetDecorators(LuauMetadataProperty binding)
    {
        var decorators = binding.decorators;
        Dictionary<string, List<LuauMetadataDecoratorValue>> result = new();
        for (var i = 0; i < decorators.Count; i++)
        {
            var element = decorators[i];
            var decoratorName = element.name;
            var paramsProperty = element.parameters;
            result[decoratorName] = paramsProperty;
        }

        return result;
    }

    private void TryDrawDecorator(String name, List<LuauMetadataDecoratorValue> parameters)
    {
        switch (name)
        {
            case "Header":
                EditorGUILayout.Space();
                EditorGUILayout.LabelField((String) parameters[0].value, EditorStyles.boldLabel);
                return;
            case "Spacing":
                if (parameters.Count == 0)
                {
                    EditorGUILayout.Space();
                }
                else
                {
                    EditorGUILayout.Space(Convert.ToSingle(parameters[0].value));
                }
                return;
        }
    }
    
    private string GetTooltip(string comment, Dictionary<string, List<LuauMetadataDecoratorValue>> decorators)
    {
        // First try to grab tooltip from decorators 
        if (decorators.TryGetValue("Tooltip", out var tooltipProp))
        {
            var arrayIndex = tooltipProp[0];
            return arrayIndex.value.ToString();
        }
        // Fallback to using comment as tooltip
        return comment;
    }
}
#endif
