using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;

namespace Airship.Editor {
    public enum TypescriptEditor {
        SystemDefined,
        VisualStudioCode,
        Custom,
    }
    
    public enum TypescriptCompilerVersion {
        UseEditorVersion,
        UseProjectVersion,
#if !AIRSHIP_INTERNAL
        [Obsolete]
#endif
        UseLocalDevelopmentBuild,
    }
    
    public class TypescriptPopupWindow : PopupWindowContent {
        private static GUIStyle MenuItemIcon = new GUIStyle("LargeButtonMid") {
            fontSize = 13,
            fixedHeight = 25,
            fixedWidth = 0,
            padding = new RectOffset(5, 5, 0, 0),
            margin = new RectOffset(5, 5, 2, 2),
            imagePosition = ImagePosition.ImageLeft,
            alignment = TextAnchor.MiddleLeft,
        };
        
        private static GUIStyle MenuItem = new GUIStyle("LargeButtonMid") {
            fontSize = 13,
            fixedHeight = 25,
            stretchWidth = true,
            fixedWidth = 0,
            padding = new RectOffset(5, 5, 0, 0),
            margin = new RectOffset(5, 5, 5, 5),
            imagePosition = ImagePosition.ImageLeft,
            alignment = TextAnchor.MiddleLeft,
        };

        private static Texture BuildIcon = EditorGUIUtility.Load("d_CustomTool") as Texture;
        private static Texture PlayIcon = EditorGUIUtility.Load("d_PlayButton") as Texture;
        private static Texture PlayIconOn = EditorGUIUtility.Load("PlayButton On") as Texture;
        private static Texture StopIcon = EditorGUIUtility.Load("d_StopButton") as Texture;
        private static Texture RevealIcon = EditorGUIUtility.Load("d_CustomTool") as Texture;
        private static Texture SettingsIcon = EditorGUIUtility.Load("d_SettingsIcon") as Texture;

        public override Vector2 GetWindowSize() {
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
                                if (GUILayout.Button(new GUIContent("Stop Watch", StopIcon, "Stop the TypeScript compiler watching for changes in this project"), MenuItemIcon)) {
                                    TypescriptCompilationService.StopCompilers(project);
                                }
                            }
                            else if (project.HasCompiler) {
                                if (GUILayout.Button(new GUIContent("Start Watch", PlayIcon, "Start the TypeScript compiler to compile any changes to this project"), MenuItemIcon)) {
                                    //TypescriptCompilationService.StartCompilers(project);
                                }
                            }

                            GUI.enabled = compilerProcess is not { IsActive: true };
                            if (GUILayout.Button(new GUIContent(" Build", BuildIcon, GUI.enabled ? "Build this TypeScript project and generate types (if applicable)" : "Watch mode must be disabled to build this project"),
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
                        
                        if (GUILayout.Button(new GUIContent(text, RevealIcon), MenuItemIcon)) {
                            EditorUtility.RevealInFinder(Path.Join(project.Directory, "tsconfig.json"));
                        }
                        
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Separator();
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
                            new GUIContent(" Stop TypeScript", StopIcon, "Stops TypeScript from automatically compiling the projects it is watching"),
                            MenuItem)) {
                        TypescriptCompilationService.StopCompilers();
                    }
                }
                else {
                    if (GUILayout.Button(
                            new GUIContent(" Start TypeScript", PlayIconOn, "Start TypeScript automatically compiling all projects when changed"),
                            MenuItem)) {
                        TypescriptCompilationService.StartCompilerServices();
                    }
                }
                
                if (GUILayout.Button(
                        new GUIContent(" Build", BuildIcon, "Run a full build of the TypeScript code + generate types for the project(s) - will stop any active compilers"),
                        MenuItem)) {
                    if (TypescriptCompilationService.IsWatchModeRunning) {
                        TypescriptCompilationService.StopCompilerServices();
                        TypescriptCompilationService.CompileTypeScript(new []{ TypescriptProjectsService.Project });
                        TypescriptCompilationService.StartCompilerServices();
                    }
                    else {
                        TypescriptCompilationService.CompileTypeScript(new []{ TypescriptProjectsService.Project });
                    }
                }
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button(
                    new GUIContent(" Configure", SettingsIcon, "Configure the TypeScript projects or settings around the TypeScript services"),
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
            window.Focus();
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
                settings.compilerVersion = (TypescriptCompilerVersion) EditorGUILayout.EnumPopup(
                    new GUIContent("Editor Compiler", "The editor TypeScript files will be opened with"), 
                    settings.compilerVersion,
                    (version) => {
                        switch ((TypescriptCompilerVersion)version) {
                            case TypescriptCompilerVersion.UseEditorVersion:
                                return true;
#if AIRSHIP_INTERNAL
                            case TypescriptCompilerVersion.UseLocalDevelopmentBuild: {
                                return true;
                            }
#else
                            case TypescriptCompilerVersion.UseLocalDevelopmentBuild: 
                                return false;
#endif
                            case TypescriptCompilerVersion.UseProjectVersion:
                                return TypescriptProjectsService.Project?.Package.GetDependencyInfo(
                                        "@easy-games/unity-ts") != null;
                            default:
                                return false;
                        }
                    },
                    false
                );

                if (settings.compilerVersion == TypescriptCompilerVersion.UseProjectVersion) {
                    var version = TypescriptProjectsService.Project?.Package.GetDependencyInfo("@easy-games/unity-ts");
                    if (version != null) {
                        EditorGUILayout.LabelField("Version", version.Version);
                    }
                }
                
                settings.typescriptVerbose = EditorGUILayout.ToggleLeft(new GUIContent("Verbose", "Will display much more verbose information when compiling a TypeScript project"),  settings.typescriptVerbose );
                
                #if AIRSHIP_INTERNAL
                settings.typescriptWriteOnlyChanged = EditorGUILayout.ToggleLeft(new GUIContent("Write Only Changed", "Will write only changed files (this shouldn't be enabled unless there's a good reason for it)"), settings.typescriptWriteOnlyChanged);
                settings.typescriptUseDevBuild =
                    EditorGUILayout.ToggleLeft(new GUIContent("Use Development Compiler (utsc-dev)"), settings.typescriptUseDevBuild);
                #endif    
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Editor Options", EditorStyles.boldLabel);
            {
                settings.typescriptEditor = (TypescriptEditor) EditorGUILayout.EnumPopup(
                    new GUIContent("TypeScript Editor", "The editor TypeScript files will be opened with"), 
                    settings.typescriptEditor,
                    (item) => {
                        return (TypescriptEditor)item switch {
                            TypescriptEditor.VisualStudioCode => TypescriptProjectsService.VSCodePath != null,
                            _ => true
                        };
                    },
                    false
                    );
                if (settings.typescriptEditor == TypescriptEditor.Custom) {
                    settings.typescriptEditorCustomPath = EditorGUILayout.TextField(new GUIContent("TS Editor Path"),
                        settings.typescriptEditorCustomPath);
                    // EditorGUILayout.HelpBox("This should be a path to to the executable.\nUse {path}", MessageType.Info, true);
                    EditorGUILayout.HelpBox(new GUIContent(string.Join("\n", args)), false);
                } else if (settings.typescriptEditor == TypescriptEditor.VisualStudioCode) {
                    EditorGUILayout.LabelField("Editor Path", TypescriptProjectsService.VSCodePath);
                }
            }
            
            EditorGUI.indentLevel -= 1;
        }
        
        private int selectedTab = 1;
        private void OnGUI() {
            EditorGUILayout.Space(5);

            if (this.selectedTab == 1) {
                this.showSettings = EditorGUILayout.Foldout(this.showSettings, new GUIContent("Typescript Settings"), true, EditorStyles.foldoutHeader);
                if (this.showSettings) {
                    AirshipEditorGUI.HorizontalLine();

                    RenderSettings();
                    
                    if (GUI.changed) {
                        EditorIntegrationsConfig.instance.Modify();
                    }
                    
                    AirshipEditorGUI.HorizontalLine();
                }
            }
            
            if (this.selectedTab == 0) {
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
            }
        }
    }
}