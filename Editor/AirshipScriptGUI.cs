#if UNITY_EDITOR
using System;
using JetBrains.Annotations;
using Luau;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.Luau {
    public static class AirshipScriptGUI {
        internal delegate Object AirshipBehaviourValidator(Object[] references, SerializedProperty property);

        private static Object Validate(Object[] references, AirshipScript script, AirshipComponent binding) {
            if (references.Length <= 0) return null;

            var buildInfo = AirshipBuildInfo.Instance;
            
            var objectReference = references[0];
            if (objectReference is GameObject gameObject) {
                references = gameObject.GetComponents(typeof(AirshipComponent));
                foreach (var reference in references) {
                    if (reference != null && reference is AirshipComponent bindingComponent &&
                        (bindingComponent.IsBindableAsComponent(script) ||
                         buildInfo.ComponentIsValidInheritance(bindingComponent, script))) {
                        return reference;
                    }
                }
            }
            else if (objectReference is AirshipComponent otherBinding && (otherBinding.IsBindableAsComponent(script) || buildInfo.ComponentIsValidInheritance(otherBinding, script))) {
                return otherBinding;
            }
            
            return null;
        }
        
        private static AirshipComponent DoAirshipBehaviourField(
            Rect position, 
            Rect dropRect, 
            int id, 
            AirshipScript script, 
            [CanBeNull] AirshipComponent airshipComponent, 
            SerializedProperty property,
            bool allowSceneObjects,
            Action<AirshipComponent> onObjectSelected = null,
            Action onObjectRemoved = null) {
            if (!script) {
                EditorGUI.HelpBox(position, "script == null", MessageType.Error);
                return null;
            }
            
            Event evt = Event.current;
            EventType eventType = evt.type;

            var obj = (Object) property?.objectReferenceValue ?? airshipComponent;

            if (eventType == EventType.ContextClick && position.Contains(Event.current.mousePosition)) {
                var contextMenu = new GenericMenu();
                
                if (property != null) {
                    contextMenu.AddItem(new GUIContent("Properties..."), false, () => {
                        EditorUtility.OpenPropertyEditor(obj);
                    });
                }
                
                contextMenu.DropDown(position);
                Event.current.Use();
            }
            
            var buttonStyle = new GUIStyle("ObjectFieldButton");
            var buttonRect =
                buttonStyle.margin.Remove(new Rect(position.xMax - 19, position.y, 19, position.height));

            switch (eventType) {
                case EventType.DragUpdated:
                case EventType.DragPerform: {
                    // Handle drag over lol

                    if (dropRect.Contains(Event.current.mousePosition) && GUI.enabled) {
                        // if dropping something on this
                        var references = DragAndDrop.objectReferences;

                        var validatedObject = Validate(references, script, airshipComponent);
                        if (validatedObject != null) {
                            if (!allowSceneObjects && !EditorUtility.IsPersistent(validatedObject)) {
                                validatedObject = null;
                            }

                            if (DragAndDrop.visualMode == DragAndDropVisualMode.None)
                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                            if (eventType == EventType.DragPerform) {
                                obj = validatedObject;

                                GUI.changed = true;
                                DragAndDrop.AcceptDrag();
                                DragAndDrop.activeControlID = 0;
                            }
                            else {
                                DragAndDrop.activeControlID = id;
                            }

                            Event.current.Use();
                        }
                    }

                    break;
                }
                case EventType.MouseDown: {
                    if (buttonRect.Contains(Event.current.mousePosition)) {
                        if (GUI.enabled) {
                            GUIUtility.keyboardControl = id;

                            var searchContext = SearchService.CreateContext(string.Empty);
                            var view = SearchService.ShowPicker(
                                searchContext,
                                (item, b) => {
                                    var obj = item.ToObject<AirshipComponent>();
                                    if (obj != null) {
                                        onObjectSelected?.Invoke(obj);
                                    }

                                    GUI.changed = true;
                                },
                                item => {
                                    var obj = item.ToObject<AirshipComponent>();
                                    if (obj != null) {
                                        EditorGUIUtility.PingObject(obj);
                                    }
                                },
                                item => {
                                    var itemScriptBinding = item.ToObject<AirshipComponent>();
                                    return itemScriptBinding != null &&
                                           itemScriptBinding.IsBindableAsComponent(script);
                                }, null, script.m_metadata?.displayName ?? "AirshipBehaviour");
                            view.SetSearchText($"h: t:AirshipComponent #scriptFile=\"{script.assetPath}\""); // #m_fileFullPath={script.m_path}

                            evt.Use();
                            GUIUtility.ExitGUI();
                        }
                    }
                    else if (Event.current.button == 0 && position.Contains(Event.current.mousePosition)) {
                        var actualTarget = property != null ? property.objectReferenceValue : obj;
                        var component = actualTarget as AirshipComponent;
                        if (component) {
                            actualTarget = component.gameObject;
                        }
                        
                        switch (Event.current.clickCount) {
                            case 1:
                                EditorGUIUtility.PingObject(actualTarget);
                                evt.Use();
                                break;
                            case 2: {
                                if (actualTarget) {
                                    AssetDatabase.OpenAsset(actualTarget);
                                    evt.Use();
                                    GUIUtility.ExitGUI();
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
                case EventType.Repaint: {
                    var temp = EditorGUIUtility.ObjectContent(obj, typeof(AirshipComponent));

                    var scriptInfo = airshipComponent && airshipComponent.scriptFile ? airshipComponent.scriptFile : script;
                    
                    var displayName = scriptInfo.m_metadata != null && !string.IsNullOrEmpty(scriptInfo.m_metadata.displayName)
                        ? scriptInfo.m_metadata.displayName
                        : ObjectNames.NicifyVariableName(script.name);
                    
                    temp.text = obj == null
                        ? $"None ({displayName})"
                        : $"{obj.name} ({displayName})";

                    var displayIcon = scriptInfo.m_metadata?.displayIcon;
                    
                    if (displayIcon) {
                        temp.image = displayIcon;
                    }
                    
                    EditorStyles.objectField.Draw(position, temp, id, DragAndDrop.activeControlID == id,
                        position.Contains(Event.current.mousePosition));



                    buttonStyle.Draw(buttonRect, GUIContent.none, id, DragAndDrop.activeControlID == id,
                        buttonRect.Contains(Event.current.mousePosition));

                    break;
                }
                case EventType.KeyDown: {
                    if (GUIUtility.keyboardControl == id) {
                        if (evt.keyCode == KeyCode.Backspace || (evt.keyCode == KeyCode.Delete &&
                                                                 (evt.modifiers & EventModifiers.Shift) == 0)) {
                            obj = null;
                        }
                        
                        GUI.changed = true;
                        evt.Use();
                    }
                    break;
                }
            }

            return (AirshipComponent) obj;
        }
        
        internal static AirshipComponent AirshipBehaviourField(Rect rect, GUIContent content, AirshipScript script, AirshipComponent airshipComponent, SerializedProperty property) {
            int id = GUIUtility.GetControlID("_airshipBehaviourFieldHash".GetHashCode(), FocusType.Keyboard, rect);
            
            rect = EditorGUI.PrefixLabel(rect, id, content);
            var value = DoAirshipBehaviourField(
                rect, rect, id, script, airshipComponent, property, true,
                binding => {
                    if (property != null) {
                        property.objectReferenceValue = binding;
                    }
                },
                () => {
                    Debug.Log("Remove object " + property.objectReferenceValue);
                    property.objectReferenceValue = null;
                });
            
            return value;
        }

        internal static AirshipComponent AirshipBehaviourField(GUIContent content, AirshipScript script, SerializedProperty property) {
            var r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, script, (AirshipComponent) property.objectReferenceValue, property);
        }
        
        public static AirshipComponent AirshipBehaviourField(GUIContent content, AirshipScript script, AirshipComponent airshipComponent) {
            var r = EditorGUILayout.GetControlRect(false, ObjectField.singleLineHeight);
            return AirshipBehaviourField(r, content, script, airshipComponent, null);
        }
    }
}
#endif