using System;
using System.Linq;
using Editor.EditorInternal;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airship.Editor {
#if AIRSHIP_EXPERIMENT_COMPONENT_BUTTON
    [InitializeOnLoad] // lol
#endif
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
            var inspectorWindowArray = TryGetInspectorWindows();
            if (inspectorWindowArray.Length == 0) return;
            
            foreach (var inspectorWindow in inspectorWindowArray) {
                if (!inspectorWindow.hasFocus) continue;
                AddCustomButton(inspectorWindow);
            }
     
            EditorApplication.delayCall += OnUpdate;
        }

        public static IMGUIContainer CreateIMGUIContainer(Action onGUIHandler, string name = null) {
            IMGUIContainer result = null;
            result = new IMGUIContainer(onGUIHandler);
            return result;
        }

        private static Rect buttonRect;
        private static VisualElement addAirshipComponentContainer;

        
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

            //var test = addComponentButton.Q<IMGUIContainer>(className: "unity-imgui-container");
            var test = CreateIMGUIContainer(() => {
                var activeEditor = InspectorExtensions.GetFirstNonImportInspectorEditor(ActiveEditorTracker.sharedTracker.activeEditors);
                if (activeEditor.target is not GameObject) {
                    return;
                }
                
                EditorGUILayout.BeginHorizontal(GUILayout.Height(40));
                {
                    GUILayout.FlexibleSpace();
                    var content = new GUIContent("Add Airship Component");
                    var rect = GUILayoutUtility.GetRect(content, "AC Button");
                    // rect.y -= 9;

                    if (EditorGUI.DropdownButton(rect, content, FocusType.Passive, "AC Button")) {
                        AirshipComponentDropdown dd = new AirshipComponentDropdown(new AdvancedDropdownState(),
                            (binaryFile) => {
                                var targetGo = activeEditor.target as GameObject;
                                if (!targetGo) return;
                                
                                var binding = targetGo.AddComponent<AirshipComponent>();
                                binding.SetScript(binaryFile);
                                
                                EditorUtility.SetDirty(targetGo);
                            });
                        dd.Show(rect, 300);
                    }

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            });

            addAirshipComponentContainer = new VisualElement();
            addAirshipComponentContainer.AddToClassList(CustomButtonClassName);
            addAirshipComponentContainer.Add(test);
            
            addComponentButton.parent.Add(addAirshipComponentContainer);
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