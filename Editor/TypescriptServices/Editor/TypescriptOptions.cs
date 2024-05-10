using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;

namespace Airship.Editor {
    public enum TypescriptEditor {
        VisualStudioCode,
        Custom,
    }
    
    public class TypescriptPopupWindow : PopupWindowContent {
        public static GUIStyle MenuItemIcon = new GUIStyle("LargeButtonMid") {
            fontSize = 13,
            fixedHeight = 25,
            fixedWidth = 0,
            padding = new RectOffset(5, 5, 0, 0),
            margin = new RectOffset(5, 5, 2, 2),
            imagePosition = ImagePosition.ImageLeft,
            alignment = TextAnchor.MiddleLeft,
        };
        
        public static GUIStyle MenuItem = new GUIStyle("LargeButtonMid") {
            fontSize = 13,
            fixedHeight = 25,
            stretchWidth = true,
            fixedWidth = 0,
            padding = new RectOffset(5, 5, 0, 0),
            margin = new RectOffset(5, 5, 5, 5),
            imagePosition = ImagePosition.ImageLeft,
            alignment = TextAnchor.MiddleLeft,
        };

        public override Vector2 GetWindowSize() {
            // TypescriptProjectsService.ReloadProjects();
            // var projects = TypescriptProjectsService.Projects;
            // var projectCount = projects.Count;
            //
            // var wsize = base.GetWindowSize();
            // return new Vector2(400, 80 + 11 + (11 * projectCount) + 60 * projectCount);
            return new Vector2(400, 80 + 11);
        }

        internal static void RenderProjects() { 
            var projects = TypescriptProjectsService.Projects;
            AirshipEditorGUI.HorizontalLine();
            
            foreach (var project in projects) {
                var packageJson = project.Package;
                if (packageJson == null) continue;
                
                var servicesState = TypescriptCompilationServicesState.instance;
                var compilerProcess = servicesState.GetWatchStateForDirectory(project.Directory);
                
                EditorGUILayout.BeginHorizontal(GUILayout.Height(50), GUILayout.Width(380));
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.LabelField(
                            packageJson.Name.StartsWith("@") ? $"{packageJson.Name} (Package)" : packageJson.Name,
                            new GUIStyle(EditorStyles.largeLabel) {
                                fontStyle = FontStyle.Bold
                            });

                        if (compilerProcess != null) {
                            if (compilerProcess.IsCompiling) {
                                EditorGUILayout.LabelField(
                                    $"Compiling...",
                                    new GUIStyle() {
                                        normal = new GUIStyleState() {
                                            textColor = new Color(1, 1, 0.4f)
                                        }
                                    });
                            }
                            else if (compilerProcess.HasErrors) {
                                EditorGUILayout.LabelField(
                                    $"{compilerProcess.ErrorCount} compilation {(compilerProcess.ErrorCount != 1 ? "errors" : "error")}",
                                    new GUIStyle() {
                                        normal = new GUIStyleState() {
                                            textColor = new Color(1, 0.4f, 0)
                                        }
                                    });
                            }
                            else {
                                EditorGUILayout.LabelField(
                                    $"Compilation OK",
                                    new GUIStyle() {
                                        normal = new GUIStyleState() {
                                            textColor = new Color(0.2f, 1f, 0)
                                        }
                                    });
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Separator();
                    //EditorGUILayout.BeginVertical();

                    EditorGUILayout.BeginVertical(); 
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            if (compilerProcess != null && compilerProcess.IsActive) {
                                if (GUILayout.Button(new GUIContent("Stop Watch",
                                        EditorGUIUtility.Load("d_StopButton") as Texture, "Stop the TypeScript compiler watching for changes in this project"), MenuItemIcon)) {
                                    TypescriptCompilationService.StopCompilers(project);
                                }
                            }
                            else if (project.HasCompiler) {
                                if (GUILayout.Button(new GUIContent("Start Watch",
                                        EditorGUIUtility.Load("d_PlayButton") as Texture, "Start the TypeScript compiler to compile any changes to this project"), MenuItemIcon)) {
                                    //TypescriptCompilationService.StartCompilers(project);
                                }
                            }

                            GUI.enabled = compilerProcess is not { IsActive: true };
                            if (GUILayout.Button(new GUIContent(" Build", EditorGUIUtility.Load("d_CustomTool") as Texture, GUI.enabled ? "Build this TypeScript project and generate types (if applicable)" : "Watch mode must be disabled to build this project"),
                                    MenuItemIcon)) {
                                // TypescriptCompilationService.CompileTypeScriptProject(project.Directory, TypeScriptCompileFlags.DisplayProgressBar);
                               EditorUtility.ClearProgressBar(); 
                            }
                            GUI.enabled = true;
                        }
                        EditorGUILayout.EndHorizontal();

                        var text = " View Files";
                        #if UNITY_EDITOR_OSX
                            text = " Reveal in Finder";
                        #elif UNITY_EDITOR_WIN
                            text = " Reveal in Explorer";
                        #endif
                        
                        if (GUILayout.Button(new GUIContent(text, EditorGUIUtility.Load("d_CustomTool") as Texture), MenuItemIcon)) {
                            EditorUtility.RevealInFinder(Path.Join(project.Directory, "tsconfig.json"));
                        }
                        
                    }
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Separator();
                   // EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
                
                AirshipEditorGUI.HorizontalLine();
            }
        }

        public override void OnGUI(Rect rect) {
            EditorGUILayout.LabelField("TypeScript", new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold});

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            {
                var compilerCount = TypescriptCompilationService.WatchCount;
                if (compilerCount > 0) {
                    if (GUILayout.Button(
                            new GUIContent(" Stop TypeScript", EditorGUIUtility.Load("StopButton") as Texture, "Stops TypeScript from automatically compiling the projects it is watching"),
                            MenuItem)) {
                        TypescriptCompilationService.StopCompilers();
                    }
                }
                else {
                    if (GUILayout.Button(
                            new GUIContent(" Start TypeScript", EditorGUIUtility.Load("PlayButton On") as Texture, "Start TypeScript automatically compiling all projects when changed"),
                            MenuItem)) {
                        TypescriptCompilationService.StartCompilerServices();
                    }
                }
                
                if (GUILayout.Button(
                        new GUIContent(" Build", EditorGUIUtility.Load("d_CustomTool") as Texture, "Run a full build of the TypeScript code + generate types for the project(s) - will stop any active compilers"),
                        MenuItem)) {
                    // TypescriptCompilationService.StopCompilerServices();
                    // TypescriptCompilationService.CompileTypeScript();
                }
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button(
                    new GUIContent(" Configure", EditorGUIUtility.Load("d_SettingsIcon") as Texture, "Configure the TypeScript projects or settings around the TypeScript services"),
                    MenuItem)) {
                TypescriptOptions.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
    
    [EditorWindowTitle(title = "TypeScript Configuration")]
    public class TypescriptOptions : EditorWindow {

        
        public static void ShowWindow() {
            var window = GetWindow(typeof(TypescriptOptions));
            window.titleContent = new GUIContent("TypeScript", CompileTypeScriptButton.typescriptIconOff);
            window.Show();
            TypescriptServicesStatusWindow.Open();
        }
    
        private bool showProjects = true;
        private bool showSettings = true;

        private Vector2 scrollPosition;
        private Rect area;

        private static string[] args = {
            "This should be the path of the editor you want to open TypeScript files with",
            "-- included variables: --",
            "{filePath} - The path of the file",
            "{line} - The line of the file",
            "{column} - The column of the file"
        };

        internal static void RenderSettings() {
            var settings = EditorIntegrationsConfig.instance;

            EditorGUI.indentLevel += 1;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Service Options", EditorStyles.boldLabel);
            {
                settings.typescriptAutostartCompiler =
                    EditorGUILayout.ToggleLeft(new GUIContent("Automatically Run on Editor Startup", "Compilation of TypeScript files will be handled by the editor"), settings.typescriptAutostartCompiler);
                settings.typescriptPreventPlayOnError =
                    EditorGUILayout.ToggleLeft(new GUIContent("Prevent Play Mode With Errors", "Stop being able to go into play mode if there are active compiler errors"), settings.typescriptPreventPlayOnError);
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Compiler Options", EditorStyles.boldLabel);
            {
                settings.typescriptVerbose = EditorGUILayout.ToggleLeft(new GUIContent("Verbose", "Will display much more verbose information when compiling a TypeScript project"),  settings.typescriptVerbose );
                
                #if AIRSHIP_INTERNAL && UNITY_EDITOR_WIN
                settings.typescriptWriteOnlyChanged = EditorGUILayout.ToggleLeft(new GUIContent("Write Only Changed", "Will write only changed files (this shouldn't be enabled unless there's a good reason for it)"), settings.typescriptWriteOnlyChanged);
                settings.typescriptUseDevBuild =
                    EditorGUILayout.ToggleLeft(new GUIContent("Use Development Compiler (utsc-dev)"), settings.typescriptUseDevBuild);
                #endif    
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Editor Options", EditorStyles.boldLabel);
            {
                settings.typescriptEditor = (TypescriptEditor) EditorGUILayout.EnumPopup(new GUIContent("TypeScript Editor", "The editor TypeScript files will be opened with"), settings.typescriptEditor);
                if (settings.typescriptEditor == TypescriptEditor.Custom) {
                    settings.typescriptEditorCustomPath = EditorGUILayout.TextField(new GUIContent("TS Editor Path"),
                        settings.typescriptEditorCustomPath);
                    // EditorGUILayout.HelpBox("This should be a path to to the executable.\nUse {path}", MessageType.Info, true);
                    EditorGUILayout.HelpBox(new GUIContent(string.Join("\n", args)), false);
                }
            }
            
            EditorGUI.indentLevel -= 1;
        }
        
        private int selectedTab = 1;
        private void OnGUI() {
            EditorGUILayout.Space(5);
            // EditorGUILayout.BeginHorizontal();
            // GUILayout.FlexibleSpace();
            // this.selectedTab = GUILayout.Toolbar(this.selectedTab, new string[] {"Projects", "Settings"}, GUILayout.Width(300));
            // GUILayout.FlexibleSpace();
            // EditorGUILayout.EndHorizontal();
            //
            // AirshipEditorGUI.HorizontalLine();

            if (this.selectedTab == 1) {
                this.showSettings = EditorGUILayout.Foldout(this.showSettings, new GUIContent("Typescript Settings"), true,EditorStyles.foldoutHeader);
                if (this.showSettings) {
                    AirshipEditorGUI.HorizontalLine();

                    RenderSettings();
                    
                    if (GUI.changed) {
                        EditorIntegrationsConfig.instance.Modify();
                    }
                    
                    AirshipEditorGUI.HorizontalLine();
                }
            }
            
            if (this.selectedTab == 0){
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Refresh")) {
                        TypescriptProjectsService.ReloadProjects();
                    }
                    if (GUILayout.Button("Update All")) {
                        TypescriptProjectsService.UpdateTypescript();
                    }
                }
                EditorGUILayout.EndHorizontal();
                
            this.showProjects = EditorGUILayout.Foldout(this.showProjects, new GUIContent("Typescript Projects"), true,EditorStyles.foldoutHeader);
            if (this.showProjects) {
                EditorGUI.indentLevel += 1;
                
                var projects = TypescriptProjectsService.Projects;
                AirshipEditorGUI.HorizontalLine();

                this.scrollPosition = EditorGUILayout.BeginScrollView(this.scrollPosition);
                {
                    foreach (var project in projects) {
                        var packageJson = project.Package;

                        if (packageJson == null) {
                            continue;
                        }
                        
                        EditorGUILayout.LabelField(
                            packageJson.Name.StartsWith("@") ? $"{packageJson.Name} (Package)" : packageJson.Name,
                            new GUIStyle(EditorStyles.largeLabel) {
                                fontStyle = FontStyle.Bold,
                            });
                        EditorGUILayout.LabelField(project.Directory);
                    
                        if (!project.HasCompiler) {
                            EditorGUILayout.HelpBox("This Typescript project has issues or has not been initialized correctly", MessageType.Error, true);
                        }
                        else {
                            if (TypescriptProjectsService.MinCompilerVersion.IsNewerThan(project.CompilerVersion)) {
                                EditorGUILayout.HelpBox(
                                    "This project's compiler version is out of the recommended version range and requires updating", 
                                    MessageType.Error, 
                                    true
                                    );
                            }
                            else {
                                EditorGUILayout.BeginHorizontal(); 
                                {
                                    EditorGUILayout.LabelField("Compiler", project.CompilerVersion.ToString(), EditorStyles.whiteLabel);
         
                                    // if (GUILayout.Button("Update")) {
                                    //     foreach (var managedPackage in TypescriptProjectsService.managedPackages) {
                                    //         TypescriptProjectsService.CheckUpdateForPackage(
                                    //             new string[] { project.Directory }, managedPackage, "staging");
                                    //     }
                                    //
                                    //     EditorUtility.ClearProgressBar();
                                    // }
                                } 
                                EditorGUILayout.EndHorizontal();
                           
                                EditorGUILayout.LabelField("Types", project.CompilerTypesVersion.ToString(), EditorStyles.whiteLabel);
                                EditorGUILayout.LabelField("Flamework", project.FlameworkVersion.ToString(), EditorStyles.whiteLabel);
                            }
                        }

                        EditorGUILayout.BeginHorizontal();
                        {
                            if (!project.HasCompiler) {
                                if (GUILayout.Button("Run Setup")) {
                                    NodePackages.RunNpmCommand(project.Directory, "install @easy-games/unity-ts@staging");
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    
                        AirshipEditorGUI.HorizontalLine();
                    }   
                }
                EditorGUILayout.EndScrollView();
                
                EditorGUI.indentLevel -= 1;
            }
            }
        }
    }
}