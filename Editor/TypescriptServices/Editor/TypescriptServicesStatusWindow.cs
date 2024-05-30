using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airship.Editor {
    public enum TypescriptStatusTab {
        Problems,
    }

    static class TypeScriptStatusWindowStyle {
        public static readonly GUIStyle EntryEven;
        public static readonly GUIStyle EntryOdd;
        public static readonly GUIStyle EntryItemText = new GUIStyle() {
            richText = true,
            normal = new GUIStyleState() {
                textColor = new Color(0.8f, 0.8f, 0.8f)
            },
            font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
            fontSize = 11,
            fontStyle = FontStyle.Normal,
        };

        public static readonly GUIStyle EntryItemDetails = new GUIStyle(EntryItemText) {
            normal = new GUIStyleState() {
                textColor = new Color(0.7f, 0.7f, 0.7f)
            }
        };

        static TypeScriptStatusWindowStyle() {
            EntryEven = new GUIStyle("CN EntryBackEven") {
                margin = new RectOffset(0, 0, 0, 0),
            };
            
            EntryOdd = new GUIStyle("CN EntryBackOdd") {
                margin = new RectOffset(0, 0, 0, 0),
            };
        }
    }
    
    [EditorWindowTitle]
    public class TypescriptServicesStatusWindow : EditorWindow {
        [MenuItem("Airship/TypeScript/Show Problems Window")]
        public static void Open() {
            var window = GetWindow(typeof(TypescriptServicesStatusWindow));
            window.titleContent = new GUIContent("Typescript Services", CompileTypeScriptButton.typescriptIconOff);
            window.Show();
        }

        internal TypescriptStatusTab ActiveTab { get; set; } = TypescriptStatusTab.Problems;

        private Dictionary<string, bool> foldouts = new();

        private TypescriptProblemItem selectedProblemItem;

        public static void Reload() {
            var window = GetWindow<TypescriptServicesStatusWindow>();
            window.selectedProblemItem = null;
        }

        private Vector2 position = new Vector2();
        private void OnGUI() {
            GUILayout.BeginHorizontal("Toolbar");
            GUILayout.Toggle(ActiveTab == TypescriptStatusTab.Problems, TypescriptProjectsService.ProblemCount > 0 ? $"Problems ({TypescriptProjectsService.ProblemCount})" : "Problems", "ToolbarButtonLeft"); 
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            position = EditorGUILayout.BeginScrollView(position);
            {
                var i = 0;
                var hasSelectedItem = false;
                foreach (var project in TypescriptProjectsService.Projects) {
                    if (project.ProblemItems == null || project.ProblemItems.Count == 0) continue;
                    var foldout = foldouts.GetValueOrDefault(project.Directory, true);
                    
                  
                    
                    var foldoutRect = EditorGUILayout.GetControlRect(false, 20);
                    
                    foldout = EditorGUI.Foldout(foldoutRect, foldout, new GUIContent(project.Name), EditorStyles.foldoutHeader);
                    foldouts[project.Directory] = foldout;
                    
                    if (foldout) {
                        EditorGUI.indentLevel += 1;
                        
                        foreach (var problemItem in project.ProblemItems) {
                            EditorGUILayout.BeginHorizontal();

                            var controlRect = EditorGUILayout.GetControlRect(false, 30);


                            var isSelected = GUI.Toggle(controlRect, selectedProblemItem == problemItem, "",
                                i % 2 == 0
                                    ? TypeScriptStatusWindowStyle.EntryEven
                                    : TypeScriptStatusWindowStyle.EntryOdd);
                            
                            if (isSelected) {
                                selectedProblemItem = problemItem;
                            } else if (selectedProblemItem == problemItem) {
                                selectedProblemItem = null;
                            }


                            if (selectedProblemItem == problemItem) {
                                hasSelectedItem = true;
                            }
                            
                            GUI.Label(controlRect, new GUIContent("", problemItem.ProblemType switch {
                                TypescriptProblemType.Error => EditorGUIUtility.Load("console.erroricon"),
                                TypescriptProblemType.Warning  => EditorGUIUtility.Load("console.warnicon"),
                                _ => EditorGUIUtility.Load("console.infoicon")
                            } as Texture));

                            var labelRect = new Rect(controlRect);
                            labelRect.height = 15;
                            labelRect.x += 40;
                            
                            
                            GUI.Label(labelRect, problemItem.Message, TypeScriptStatusWindowStyle.EntryItemText);

                            labelRect.y += 15;

                            var problemText = problemItem.FileLocation;
                            if (problemItem.ErrorCode > 0) {
                                problemText += " (TS " + problemItem.ErrorCode + ")";
                            }

                            problemText +=
                                $" [Line: {problemItem.LineAndColumn.Line}, Column: {problemItem.LineAndColumn.Column}]";
                            
                            GUI.Label(labelRect, problemText, TypeScriptStatusWindowStyle.EntryItemDetails);

                            EditorGUILayout.EndHorizontal();
                            
                            i++;
                        }
                        
                        
                        EditorGUI.indentLevel -= 1;
                    }

                    
                }

                if (!hasSelectedItem) {
                    selectedProblemItem = null;
                }
            }
            EditorGUILayout.EndScrollView();

            if (selectedProblemItem != null) {
                var rect = EditorGUILayout.GetControlRect(false, 100);

                var lineRect = new Rect(rect);
                lineRect.height = 1;
                
                EditorGUI.DrawRect(rect, new Color(.22f, .22f, .22f));
                EditorGUI.DrawRect(lineRect, new Color(.16f, .16f, .16f));

                var iconRect = new Rect(rect);
                iconRect.width = 40;
                iconRect.height = 40; // lol
                GUI.Label(iconRect, new GUIContent("", EditorGUIUtility.Load("console.erroricon") as Texture));
                
                var topLine = new RectOffset(-40, 0, 0, 0).Add(rect);
                topLine.height = 23;
                GUI.Label(topLine, selectedProblemItem.Message, EditorStyles.boldLabel);

                var fullPath = Path.Join(selectedProblemItem.Project.Directory, selectedProblemItem.FileLocation).Replace("\\", "/");
                topLine.y += 15;
                topLine.height = 20;
                if (GUI.Button(topLine, $"{fullPath}:{selectedProblemItem.LineAndColumn.Line}:{selectedProblemItem.LineAndColumn.Column}", EditorStyles.linkLabel)) {
                    TypescriptProjectsService.OpenFileInEditor(fullPath, selectedProblemItem.LineAndColumn.Line, selectedProblemItem.LineAndColumn.Column);
                }
                //
                // var button = new Rect(topLine);
                // button.y += 20;
                // button.width = 200;
                // GUI.Button(button, "View File");
            }
        }
    }
}