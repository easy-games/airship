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
    
    [EditorWindowTitle(title = "Typescript")]
    public class TypescriptServicesStatusWindow : EditorWindow {
        [MenuItem("Airship/TypeScript/Show Status Window")]
        public static void Open() {
            var window = GetWindow(typeof(TypescriptServicesStatusWindow));
            window.titleContent = new GUIContent("Typescript", AirshipToolbar.typescriptIconOff);
            window.Show();
        }

        internal TypescriptStatusTab ActiveTab { get; set; } = TypescriptStatusTab.Problems;

        private Dictionary<string, bool> foldouts = new();

        private ITypescriptProblemItem _selectedDiagnosticTypescriptProblemItem;

        public static void Reload() {
            var window = GetWindow<TypescriptServicesStatusWindow>();
            window._selectedDiagnosticTypescriptProblemItem = null;
        }

        private Vector2 problemsPaneScrollPosition = new Vector2();
        private Vector2 problemsDetailsPaneScrollPosition;
        
        private readonly Vector2 padding = new Vector2(2, 2);
        private Tab problemsTab;

        private int problemCount = -1;
        
        private void UpdateProblemsTab(TypescriptProject project) {
            var count = project.ProblemItems.Count;
            if (problemCount == count) return;

            if (project.HighestProblemType is TypescriptProblemType problemType) {
                var image = problemType switch {
                    TypescriptProblemType.Fatal => "d_DebuggerEnabled@2x",
                    TypescriptProblemType.Suggestion or TypescriptProblemType.Message => "d_console.infoicon.sml",
                    TypescriptProblemType.Warning => "d_console.warnicon.sml",
                    TypescriptProblemType.Error => "d_console.erroricon.sml",
                    _ => "d_console.infoicon.sml",
                };
                problemsTab.iconImage = Background.FromTexture2D(EditorGUIUtility.Load(image) as Texture2D);
            }
            else {
                problemsTab.iconImage = Background.FromTexture2D(EditorGUIUtility.Load("d_console.warnicon.inactive.sml") as Texture2D);
            }

            problemsTab.label = count > 0 ? $"Problems ({count})" : "Problems";
            problemCount = count;
        }
        
        private void CreateGUI() {
            problemCount = -1;
            var tabView = new TabView();

            problemsTab = new Tab {
                label = "Problems",
                iconImage = Background.FromTexture2D(EditorGUIUtility.Load("console.warnicon.inactive.sml") as Texture2D)
            };
            {
                var splitView = new TwoPaneSplitView(1, 100, TwoPaneSplitViewOrientation.Vertical);
            
                var topPane = new VisualElement {
                    name = "problems-list-view",
                    style = {
                        position = Position.Relative
                    }
                };

                var problemsPane = new IMGUIContainer(OnProblemsPaneGUI);
                problemsPane.AddToClassList("problems-pane-view");
                topPane.hierarchy.Add(problemsPane);
            
                var bottomPane = new VisualElement {
                    name = "problems-details-view",
                };

                var problemDetailsPane = new IMGUIContainer(OnProblemsDetailsPaneGUI);
                problemDetailsPane.AddToClassList("problems-pane-view");
                bottomPane.hierarchy.Add(problemDetailsPane);
                
                splitView.Add(topPane);
                splitView.Add(bottomPane);
                
                splitView.AddToClassList("problems-split-view");
                problemsTab.Add(splitView);
                
                tabView.Add(problemsTab);
            }
            tabView.AddToClassList("typescript-tab-view");
            tabView.Add(problemsTab);
            
            rootVisualElement.Add(tabView);
            rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin ? "dark-mode" : "light-mode");
            rootVisualElement.styleSheets.Add( AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/gg.easy.airship/Editor/UI/TypescriptServicesWindow.uss"));

        }

        private void OnProblemsPaneGUI() {
            problemsPaneScrollPosition = EditorGUILayout.BeginScrollView(problemsPaneScrollPosition);
            {
                var i = 0;
                var hasSelectedItem = false;
                foreach (var project in TypescriptProjectsService.Projects) {
                    UpdateProblemsTab(project);
                    
                    if (project.ProblemItems == null || project.ProblemItems.Count == 0) continue;
                    EditorGUI.indentLevel += 1;
                    
                    foreach (var problemItem in project.ProblemItems) {
                        EditorGUILayout.BeginHorizontal();

                        
                        var messageContent = new GUIContent(problemItem.Message);
                        var contentHeight =
                            TypeScriptStatusWindowStyle.EntryItemDetails.CalcHeight(messageContent,
                                rootVisualElement.layout.width) + padding.y * 2;

                        if (problemItem is TypescriptFileDiagnosticItem) {
                            contentHeight += 15;
                        }
                        
                        var controlRect = EditorGUILayout.GetControlRect(false, contentHeight);
                        var prevValue = _selectedDiagnosticTypescriptProblemItem == problemItem;
                        var isSelected = GUI.Toggle(controlRect, prevValue, "",
                            i % 2 == 0
                                ? TypeScriptStatusWindowStyle.EntryEven
                                : TypeScriptStatusWindowStyle.EntryOdd);

                      
                        if (isSelected != prevValue) {
                            switch (Event.current.clickCount) {
                                case 1: {
                                    if (isSelected) {
                                        _selectedDiagnosticTypescriptProblemItem = problemItem;
                                    } else if (_selectedDiagnosticTypescriptProblemItem == problemItem) {
                                        _selectedDiagnosticTypescriptProblemItem = null;
                                    }
                                    break;
                                }
                                case 2: {
                                    if (_selectedDiagnosticTypescriptProblemItem is TypescriptFileDiagnosticItem fileDiagnostic) {
                                        var fullPath = Path.Join(problemItem.Project.Directory, fileDiagnostic.FileLocation).Replace("\\", "/");
                                        TypescriptProjectsService.OpenFileInEditor(fullPath, fileDiagnostic.LineAndColumn.Line, fileDiagnostic.LineAndColumn.Column);
                                    }
                                    break;
                                }
                            }
                        }
                        
                        if (_selectedDiagnosticTypescriptProblemItem == problemItem) {
                            hasSelectedItem = true;
                        }


                        var isDarkMode = EditorGUIUtility.isProSkin;
                        
                        GUI.Label(controlRect, new GUIContent("", problemItem.ProblemType switch {
                            TypescriptProblemType.Fatal => EditorGUIUtility.Load("d_DebuggerEnabled@2x") as Texture2D,
                            TypescriptProblemType.Error => EditorGUIUtility.Load(isDarkMode ? "d_console.erroricon" : "console.erroricon"),
                            TypescriptProblemType.Warning  => EditorGUIUtility.Load(isDarkMode ? "d_console.warnicon" : "console.warnicon"),
                            _ => EditorGUIUtility.Load(isDarkMode ? "d_console.infoicon" : "console.infoicon")
                        } as Texture));
                        
                        var labelRect = new Rect(controlRect);
                        labelRect.height = 30;
                        labelRect.x += 40;
                        
                        GUI.TextArea(labelRect, problemItem.Message, TypeScriptStatusWindowStyle.EntryItemText);
                        

                        if (problemItem is TypescriptFileDiagnosticItem fileProblemItem) {
                            var problemText = fileProblemItem.FileLocation;
                            if (problemItem.ErrorCode > 0) {
                                problemText += " (TS " + problemItem.ErrorCode + ")";
                            }

                            problemText +=
                                $" [Line: {fileProblemItem.LineAndColumn.Line}, Column: {fileProblemItem.LineAndColumn.Column}]";

                            labelRect.height = 15;
                            labelRect.y = controlRect.yMax - 15;
                            GUI.Label(labelRect, problemText, TypeScriptStatusWindowStyle.EntryItemDetails);
                            
                            EditorGUILayout.EndHorizontal();
                        } else if (problemItem is TypescriptCrashProblemItem crashDiagnostic) {
                            EditorGUILayout.EndHorizontal();
                            
                            var extraRect = EditorGUILayout.GetControlRect(false, 20);
                            
                            extraRect.width /= 2;
                            if (GUI.Button(extraRect, "Copy Details")) {
                                EditorGUIUtility.systemCopyBuffer = $"=== Airship Typescript Crash Diagnostic ===\n" +
                                                                    $"{string.Join("\n", crashDiagnostic.StandardError)}\n" +
                                                                    $"----------------- END ---------------------";
                            }

                            extraRect.x += extraRect.width;
                            if (GUI.Button(extraRect, "Restart Compiler")) {
                                TypescriptCompilationService.StartCompilerServices();
                            }
                            
                        }
                        else {
                            EditorGUILayout.EndHorizontal();
                        }


                        
                        
                        i++;
                    }
                    
                    
                    EditorGUI.indentLevel -= 1;
                    //}

                    
                }

                if (!hasSelectedItem) {
                    _selectedDiagnosticTypescriptProblemItem = null;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void OnProblemsDetailsPaneGUI() {
            problemsDetailsPaneScrollPosition = EditorGUILayout.BeginScrollView(problemsDetailsPaneScrollPosition);
            {
                var problemItem = _selectedDiagnosticTypescriptProblemItem;
                if (problemItem != null) {
                    var messageContent = new GUIContent(problemItem.Message);
                    var contentHeight =
                        TypeScriptStatusWindowStyle.EntryItemDetails.CalcHeight(messageContent,
                            rootVisualElement.layout.width) + padding.y * 2;
                    
                    var rect = EditorGUILayout.GetControlRect(false, contentHeight + 20);
                    EditorGUI.DrawRect(rect, new Color(.22f, .22f, .22f));

                    var iconRect = new Rect(rect);
                    iconRect.width = 40;
                    iconRect.height = 40; // lol
                    GUI.Label(iconRect, new GUIContent("", EditorGUIUtility.Load(EditorGUIUtility.isProSkin ? "d_console.erroricon" : "console.erroricon") as Texture));


                    
                    var topLine = new RectOffset(-40, 0, 0, 0).Add(rect);
                    topLine.height = contentHeight;
                    GUI.Label(topLine, _selectedDiagnosticTypescriptProblemItem.Message, EditorStyles.boldLabel);

                    if (_selectedDiagnosticTypescriptProblemItem is TypescriptFileDiagnosticItem
                        fileDiagnostic) {
                        var fullPath = Path.Join(_selectedDiagnosticTypescriptProblemItem.Project.Directory, fileDiagnostic.FileLocation).Replace("\\", "/");
                        topLine.y += contentHeight;
                        topLine.height = 20;
                        if (GUI.Button(topLine, $"{fullPath}:{fileDiagnostic.LineAndColumn.Line}:{fileDiagnostic.LineAndColumn.Column}", EditorStyles.linkLabel)) {
                            TypescriptProjectsService.OpenFileInEditor(fullPath, fileDiagnostic.LineAndColumn.Line, fileDiagnostic.LineAndColumn.Column);
                        }
                    }
                }   
            }
            EditorGUILayout.EndScrollView();
        }
    }
}