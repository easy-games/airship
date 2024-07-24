using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Toolbars;
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
            window.titleContent = new GUIContent("Typescript Services", AirshipToolbar.typescriptIconOff);
            window.Show();
        }

        internal TypescriptStatusTab ActiveTab { get; set; } = TypescriptStatusTab.Problems;

        private Dictionary<string, bool> foldouts = new();

        private ITypescriptProblemItem _selectedDiagnosticTypescriptProblemItem;

        public static void Reload() {
            var window = GetWindow<TypescriptServicesStatusWindow>();
            window._selectedDiagnosticTypescriptProblemItem = null;
        }

        private Vector2 storedPosition = new Vector2();
        private readonly Vector2 padding = new Vector2(2, 2);
        
        private void OnGUI() {
            GUILayout.BeginHorizontal("Toolbar");
            GUILayout.Toggle(ActiveTab == TypescriptStatusTab.Problems, TypescriptProjectsService.ProblemCount > 0 ? $"Problems ({TypescriptProjectsService.ProblemCount})" : "Problems", "ToolbarButtonLeft"); 
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            storedPosition = EditorGUILayout.BeginScrollView(storedPosition);
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

                            var controlRect = EditorGUILayout.GetControlRect(false, 15 + padding.y * 2);

                            var messageContent = new GUIContent(problemItem.Message);
                            var contentHeight =
                                TypeScriptStatusWindowStyle.EntryItemDetails.CalcHeight(messageContent,
                                    controlRect.width) + padding.y * 2;
                            controlRect.height = contentHeight;
                            
                            if (problemItem is TypescriptFileDiagnosticItem) {
                                controlRect.height += 15;
                            }

                            var isSelected = GUI.Toggle(controlRect, _selectedDiagnosticTypescriptProblemItem == problemItem, "",
                                i % 2 == 0
                                    ? TypeScriptStatusWindowStyle.EntryEven
                                    : TypeScriptStatusWindowStyle.EntryOdd);
                            
                            if (isSelected) {
                                _selectedDiagnosticTypescriptProblemItem = problemItem;
                            } else if (_selectedDiagnosticTypescriptProblemItem == problemItem) {
                                _selectedDiagnosticTypescriptProblemItem = null;
                            }


                            if (_selectedDiagnosticTypescriptProblemItem == problemItem) {
                                hasSelectedItem = true;
                            }
                            

                            
                            GUI.Label(controlRect, new GUIContent("", problemItem.ProblemType switch {
                                TypescriptProblemType.Error => EditorGUIUtility.Load("console.erroricon"),
                                TypescriptProblemType.Warning  => EditorGUIUtility.Load("console.warnicon"),
                                _ => EditorGUIUtility.Load("console.infoicon")
                            } as Texture));

                  
                            
                            var labelRect = new Rect(controlRect);
                            labelRect.height = 30;
                            labelRect.x += 40;
                            
                            GUI.TextArea(labelRect, problemItem.Message, TypeScriptStatusWindowStyle.EntryItemText);
                            labelRect.y += 15;

                            if (problemItem is TypescriptFileDiagnosticItem fileProblemItem) {
                                var problemText = fileProblemItem.FileLocation;
                                if (problemItem.ErrorCode > 0) {
                                    problemText += " (TS " + problemItem.ErrorCode + ")";
                                }

                                problemText +=
                                    $" [Line: {fileProblemItem.LineAndColumn.Line}, Column: {fileProblemItem.LineAndColumn.Column}]";

                                labelRect.y = labelRect.yMax - 15;
                                GUI.Label(labelRect, problemText, TypeScriptStatusWindowStyle.EntryItemDetails);
                            }


                            EditorGUILayout.EndHorizontal();
                            
                            i++;
                        }
                        
                        
                        EditorGUI.indentLevel -= 1;
                    }

                    
                }

                if (!hasSelectedItem) {
                    _selectedDiagnosticTypescriptProblemItem = null;
                }
            }
            EditorGUILayout.EndScrollView();

            if (_selectedDiagnosticTypescriptProblemItem != null) {
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
                GUI.Label(topLine, _selectedDiagnosticTypescriptProblemItem.Message, EditorStyles.boldLabel);

                if (_selectedDiagnosticTypescriptProblemItem is TypescriptFileDiagnosticItem
                    fileDiagnostic) {
                    var fullPath = Path.Join(_selectedDiagnosticTypescriptProblemItem.Project.Directory, fileDiagnostic.FileLocation).Replace("\\", "/");
                    topLine.y += 15;
                    topLine.height = 20;
                    if (GUI.Button(topLine, $"{fullPath}:{fileDiagnostic.LineAndColumn.Line}:{fileDiagnostic.LineAndColumn.Column}", EditorStyles.linkLabel)) {
                        TypescriptProjectsService.OpenFileInEditor(fullPath, fileDiagnostic.LineAndColumn.Line, fileDiagnostic.LineAndColumn.Column);
                    }
                }
            }
        }
    }
}