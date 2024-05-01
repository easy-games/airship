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
        [MenuItem("Airship/TypeScript/Status Window")]
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
        
        private void OnGUI() {
            GUILayout.BeginHorizontal("Toolbar");
            GUILayout.Toggle(ActiveTab == TypescriptStatusTab.Problems, TypescriptProjectsService.ProblemCount > 0 ? $"Problems ({TypescriptProjectsService.ProblemCount})" : "Problems", "ToolbarButtonLeft"); 
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.BeginScrollView(new Vector2());
            {
                var i = 0;
                foreach (var project in TypescriptProjectsService.Projects) {
                    if (project.ProblemItems == null || project.ProblemItems.Count == 0) continue;
                    var foldout = foldouts.GetValueOrDefault(project.Directory, true);
                    
                    foldout = EditorGUILayout.Foldout(foldout, new GUIContent(project.PackageJson.Name));
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
                            
                            
                            GUI.Label(controlRect, new GUIContent("", EditorGUIUtility.Load("console.erroricon") as Texture));

                            var labelRect = new Rect(controlRect);
                            labelRect.height = 15;
                            labelRect.x += 40;
                            
                            
                            GUI.Label(labelRect, problemItem.Message, TypeScriptStatusWindowStyle.EntryItemText);

                            labelRect.y += 15;

                            var problemText = Path.Join(project.Directory, problemItem.FileLocation).Replace("\\", "/");
                            if (problemItem.ErrorCode != 0) {
                                problemText += " (TS " + problemItem.ErrorCode + ")";
                            }

                            problemText +=
                                $" [Line: {problemItem.Location.Line}, Column: {problemItem.Location.Column}]";
                            
                            GUI.Label(labelRect, problemText, TypeScriptStatusWindowStyle.EntryItemDetails);

                            EditorGUILayout.EndHorizontal();
                            
                            i++;
                        }
                        
                        
                        EditorGUI.indentLevel -= 1;
                    }

                    
                }
            }
            EditorGUILayout.EndScrollView();

            if (selectedProblemItem != null) {
                var rect = EditorGUILayout.GetControlRect(false, 100);

                var lineRect = new Rect(rect);
                lineRect.height = 1;
                
                EditorGUI.DrawRect(rect, new Color(.22f, .22f, .22f));
                EditorGUI.DrawRect(lineRect, new Color(.16f, .16f, .16f));
            }
        }
    }
}