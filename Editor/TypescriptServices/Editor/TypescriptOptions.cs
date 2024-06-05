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
                                return false;
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
                this.showSettings = EditorGUILayout.Foldout(
                    this.showSettings, 
                    new GUIContent("Typescript Settings"), 
                    true, 
                    EditorStyles.foldoutHeader
                    );
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
                        TypescriptProjectsService.ReloadProject();
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