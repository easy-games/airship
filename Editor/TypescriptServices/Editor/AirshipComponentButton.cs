using System;
using System.Collections.Generic;
using System.Linq;
using Editor.EditorInternal;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airship.Editor {
    [InitializeOnLoad]
    public class AirshipComponentButton {
        private static readonly string CustomButtonText = "Add Airship Component";
        private static readonly string CustomButtonClassName = "unity-inspector-my-custom-button";
        private static readonly string CustomButtonStyleSheet = "AirshipComponentButton";
     
        private static readonly string UnityInspectorClassName = "unity-inspector-main-container";
        private static readonly string AddComponentButtonClassName = "unity-inspector-add-component-button";
     
        static AirshipComponentButton() {
            Selection.selectionChanged += SelectionChanged;
            EditorApplication.delayCall += OnUpdate;
        }

        private static void SelectionChanged() {
            // OnUpdate();
        }

        private static void OnUpdate() {
            EditorApplication.delayCall += OnUpdate;
            
            var inspectorWindowArray = TryGetInspectorWindows();
            if (inspectorWindowArray.Length == 0) return;
            
            foreach (var inspectorWindow in inspectorWindowArray) {
                AddCustomButton(inspectorWindow);
            }
        }

        public static IMGUIContainer CreateIMGUIContainer(Action onGUIHandler, string name = null) {
            IMGUIContainer result = null;
            result = new IMGUIContainer(onGUIHandler);
            return result;
        }

        private static Rect buttonRect;
        private static VisualElement addAirshipComponentContainer;

        private static void AddComponentButton(IEnumerable<UnityEditor.Editor> editors) {
            var firstPropertyEditor = editors.FirstOrDefault(editor => editor.target is not AssetImporter); 
            if (firstPropertyEditor == null || firstPropertyEditor.target is not GameObject) {
                return;
            }
            
            EditorGUILayout.BeginHorizontal(GUILayout.Height(40));
            {
                GUILayout.FlexibleSpace();
                var content = new GUIContent(CustomButtonText);
                var rect = GUILayoutUtility.GetRect(content, "AC Button");
            
                try {
                    if (GUI.Button(rect, content, "AC Button")) {
                        var airshipComponentDropdown = new AirshipComponentDropdown(new AdvancedDropdownState(),
                            (airshipScript) => {
                                var targetGo = firstPropertyEditor.target as GameObject;
                                if (!targetGo) return;

                                var component = targetGo.AddComponent<AirshipComponent>();
                                component.script = airshipScript;

                                EditorUtility.SetDirty(targetGo);
                            });

                        airshipComponentDropdown.Show(rect, 300);
                        // GUIUtility.ExitGUI();
                    }
                }
                catch (Exception e) {
                    Debug.LogError($"Received {e.GetType().Name} while using AirshipComponentDropdown: " + e.Message);
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void Repaint(EditorWindow editorWindow) {
            var componentButton = GetCustomButton(editorWindow.rootVisualElement);
            if (componentButton == null) return;
            componentButton.Clear();

            var imgui = CreateIMGUIContainer(() => {
                var editors = InspectorExtensions.GetEditorsFromWindow(editorWindow);
                AddComponentButton(editors);
            });
            componentButton.Add(imgui);
        }
        
        private static void AddCustomButton(EditorWindow editorWindow)
        {
            var addComponentButton = GetAddComponentButton(editorWindow.rootVisualElement);

            if (addComponentButton == null || addComponentButton.childCount < 1) {
                return;
            }

            var componentButton = GetCustomButton(editorWindow.rootVisualElement);

            if (componentButton != null) {
                return;
            }



            addAirshipComponentContainer = new VisualElement();
            addAirshipComponentContainer.AddToClassList(CustomButtonClassName);
            addComponentButton.parent.Add(addAirshipComponentContainer);
            
            Repaint(editorWindow);
        }
     
        private static EditorWindow[] TryGetInspectorWindows()
        {
            return Resources
                .FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window.rootVisualElement.Q(className: UnityInspectorClassName) != null)
                .ToArray();
        }
     
        private static VisualElement GetAddComponentButton(VisualElement rootVisualElement)
        {
            return rootVisualElement
                .Q(className: AddComponentButtonClassName);
        }
     
        private static VisualElement GetCustomButton(VisualElement rootVisualElement)
        {
            return rootVisualElement
                .Q(className: CustomButtonClassName);
        }
    }
}