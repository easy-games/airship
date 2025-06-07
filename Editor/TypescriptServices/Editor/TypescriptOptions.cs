using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Editor.Settings;
using NUnit.Framework;
using Unity.CodeEditor;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
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
        UseLocalDevelopmentBuild,
    }

    [Flags]
    public enum TypescriptPublishFlags {
        RecompileOnCodePublish = 2,
        RecompileOnFullPublish = 1,
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

        private static readonly Texture BuildIcon = EditorGUIUtility.Load("d_CustomTool") as Texture;
        private static readonly Texture WindowIcon = EditorGUIUtility.Load("d_UnityEditor.ConsoleWindow") as Texture;
        private static readonly Texture PlayIconOn = EditorGUIUtility.Load("d_PlayButton") as Texture;
        private static readonly Texture RestartIcon = EditorGUIUtility.Load("Refresh") as Texture;
        private static readonly Texture StopIcon = EditorGUIUtility.Load("d_PauseButton On") as Texture;
        private static readonly Texture SettingsIcon = EditorGUIUtility.Load("d_SettingsIcon") as Texture;

        public override Vector2 GetWindowSize() {
            if (TypescriptCompilationService.ShowDeveloperOptions) {
                return new Vector2(420, 105 + 11);
            }
            else {
                return new Vector2(400, 80 + 11);
            }
        }
        
        public override void OnGUI(Rect rect) {
            EditorGUILayout.LabelField("TypeScript", new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold});

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                
                if (TypescriptCompilationService.IsWatchModeRunning) {
                    if (GUILayout.Button(
                            new GUIContent(" Restart TypeScript", RestartIcon, "Restarts the TypeScript compilation service"),
                            MenuItem)) {
                        TypescriptCompilationService.RestartCompilers();
                    }
                }
                else {
                    if (GUILayout.Button(
                            new GUIContent(" Start TypeScript", PlayIconOn, "Start TypeScript automatically compiling all projects when changed"),
                            MenuItem)) {
                        TypescriptCompilationService.StartCompilerServices();
                    }
                }
                
                if (TypescriptCompilationService.ShowDeveloperOptions) {
                    GUI.enabled = TypescriptCompilationService.IsWatchModeRunning;
                    if (GUILayout.Button(
                            new GUIContent(" Stop TypeScript", StopIcon, "Restarts the TypeScript compilation service"),
                            MenuItem)) {
                        TypescriptCompilationService.StopCompilers();
                    }

                    GUI.enabled = true;
                } 
                
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button(
                        new GUIContent(" Typescript Console", WindowIcon, ""),
                        MenuItem)) {
                    TypescriptServicesStatusWindow.Open();
                }

                if (TypescriptCompilationService.ShowDeveloperOptions) {
                    if (GUILayout.Button(
                            new GUIContent(" Build", BuildIcon, "Run a full build of the TypeScript code + generate types for the project(s) - will stop any active compilers"),
                            MenuItem)) {
                        var compileFlags = TypeScriptCompileFlags.FullClean | TypeScriptCompileFlags.DisplayProgressBar;
                        if (EditorIntegrationsConfig.instance.typescriptIncremental) {
                            compileFlags |= TypeScriptCompileFlags.Incremental;
                        }
                    
                        if (EditorIntegrationsConfig.instance.typescriptVerbose) {
                            compileFlags |= TypeScriptCompileFlags.Verbose;
                        }
                        
                        if (TypescriptCompilationService.IsWatchModeRunning) {
                            TypescriptCompilationService.StopCompilerServices();
                            TypescriptCompilationService.BuildTypescript(compileFlags);
                            TypescriptCompilationService.StartCompilerServices();
                        }
                        else {
                            TypescriptCompilationService.BuildTypescript(compileFlags);
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button(
                    new GUIContent(" Configure", SettingsIcon, "Configure the TypeScript projects or settings around the TypeScript services"),
                    MenuItem)) {
                SettingsService.OpenProjectSettings(AirshipScriptingSettingsProvider.Path);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    internal enum TypescriptOptionsTab {
        ProjectSettings,
        LocalSettings,
    }
    
    public static class TypescriptOptions {
        private static string[] args = {
            "This should be the path of the editor you want to open TypeScript files with",
            "-- included variables: --",
            "{filePath} - The path of the file",
            "{line} - The line of the file",
            "{column} - The column of the file"
        };
        
        private static void RenderProjectSettings() {
            var projectSettings = EditorIntegrationsConfig.instance;
            var localSettings = TypescriptServicesLocalConfig.instance;
            
            AirshipEditorGUI.BeginSettingGroup(new GUIContent("Compiler Options"));
            {
                
                var prevIncremental = projectSettings.typescriptIncremental;
                var nextIncremental = EditorGUILayout.ToggleLeft(
                    new GUIContent("Incremental Compilation",
                        "Speeds up compilation times by skipping unchanged files (This is skipped when publishing)"),
                    prevIncremental);

                projectSettings.typescriptIncremental = nextIncremental;
                if (prevIncremental != nextIncremental) {
                    TypescriptCompilationService.RestartCompilers();
                }
            

                projectSettings.typescriptVerbose = EditorGUILayout.ToggleLeft(new GUIContent("Verbose Output", "Will display much more verbose information when compiling a TypeScript project"),  projectSettings.typescriptVerbose );
                
                #if AIRSHIP_INTERNAL
                projectSettings.typescriptWriteOnlyChanged = EditorGUILayout.ToggleLeft(new GUIContent("Write Only Changed", "Will write only changed files (this shouldn't be enabled unless there's a good reason for it)"), projectSettings.typescriptWriteOnlyChanged);
                #endif    
            }
            AirshipEditorGUI.EndSettingGroup();
            
            // EditorGUILayout.Space(10);
            // EditorGUILayout.LabelField("* This property only applies to your instance of the project");
            
            if (GUI.changed) {
                projectSettings.Modify();
                localSettings.Modify();
            }
        }

        private static int STEP = 8;
        
        private static void RenderLocalSettings() {
            
            var localSettings = TypescriptServicesLocalConfig.instance;
            
            var currentCompiler = TypescriptCompilationService.CompilerVersion;
           
            
            AirshipEditorGUI.BeginSettingGroup(new GUIContent("Compiler Options"));
            if (TypescriptCompilationService.UsableVersions.Length > 1 && TypescriptCompilationService.ShowDeveloperOptions) {
                var selectedCompiler = (TypescriptCompilerVersion) EditorGUILayout.EnumPopup(
                    new GUIContent("Editor Compiler", "The compiler to use when compiling the Typescript files in your project"), 
                    currentCompiler,
                    (version) => {
                        switch ((TypescriptCompilerVersion)version) {
                            case TypescriptCompilerVersion.UseEditorVersion:
                                return File.Exists(TypescriptCompilationService.EditorCompilerPath);
                            case TypescriptCompilerVersion.UseLocalDevelopmentBuild: 
                                return TypescriptCompilationService.HasDevelopmentCompiler;
                            default:
                                return false;
                        }
                    },
                    false
                );
                    
                if (currentCompiler != selectedCompiler) {
                    TypescriptCompilationService.RestartCompilers(() => {
                        TypescriptCompilationService.CompilerVersion = selectedCompiler;
                    });
                }
            }

            var wasOverriding = localSettings.overrideMemory;
            var prev = localSettings.overrideMemoryMb;
            
            
            localSettings.overrideMemory = EditorGUILayout.ToggleLeft("Override Memory Limits", wasOverriding);
            if (localSettings.overrideMemory) {
                EditorGUILayout.BeginHorizontal();

                localSettings.overrideMemoryMb = STEP * (EditorGUILayout.IntSlider(new GUIContent("Old Space Size (MB)"),
                    prev, 2048, SystemInfo.systemMemorySize - 512) / STEP);
                if (GUILayout.Button("Use Recommended", GUILayout.Width(150))) {
                    localSettings.overrideMemoryMb = Math.Clamp(SystemInfo.systemMemorySize - 512, 0, 8192);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            var useNodeInspect = localSettings.useNodeInspect;
            if (TypescriptCompilationService.CompilerVersion == TypescriptCompilerVersion.UseLocalDevelopmentBuild) {
                localSettings.useNodeInspect = EditorGUILayout.ToggleLeft("Run Inspector", useNodeInspect);
            }
            
            if (localSettings.overrideMemoryMb != prev || wasOverriding != localSettings.overrideMemory || useNodeInspect != localSettings.useNodeInspect) {
                // Force restart
                TypescriptCompilationService.RestartCompilers();
            }
            
            
            AirshipEditorGUI.EndSettingGroup();
            
            AirshipEditorGUI.BeginSettingGroup(new GUIContent("Editor Options"));
            {
                GUILayout.Space(5);
                GUILayout.Label("Post-Compilation", EditorStyles.boldLabel);
                
                var useExperimentalCompilation = TypescriptServices.UseShortcircuitLuauCompilation;
                TypescriptServices.UseShortcircuitLuauCompilation = EditorGUILayout.ToggleLeft(
                    new GUIContent("Compile Luau on post-compile instead of reimport"), useExperimentalCompilation);
                if (useExperimentalCompilation != TypescriptServices.UseShortcircuitLuauCompilation) {
                    TypescriptCompilationService.StopCompilerServices(shouldRestart: true);
                }
                
                GUILayout.Space(5);
                GUILayout.Label("Play Mode", EditorStyles.boldLabel);
                
                TypescriptCompilationService.PreventPlayModeWithErrors =
                    EditorGUILayout.ToggleLeft(
                        new GUIContent("Prevent Play Mode With Errors", "Stop being able to go into play mode if there are active compiler errors"), 
                        TypescriptCompilationService.PreventPlayModeWithErrors
                        );
                
                
                GUILayout.Space(5);
                GUILayout.Label("File Association", EditorStyles.boldLabel);
                
                List<CodeEditor.Installation> installations = new();
                
                installations.Add(new CodeEditor.Installation() {
                    Name = "Open by file extension",
                    Path = ""
                });
                
                int currentIndex = -1;

                if (AirshipExternalCodeEditor.CurrentEditorPath == "") {
                    currentIndex = 0;
                }
                
                foreach (var editor in AirshipExternalCodeEditor.Editors) {
                    if (editor.Installations == null) continue;

                    int idx = 1;
                    foreach (var installation in editor.Installations) {
                        installations.Add(installation);
                        if (installation.Path == AirshipExternalCodeEditor.CurrentEditorPath) {
                            currentIndex = idx;
                        }

                        idx++;
                    }
                }
                
                var selectedIdx = EditorGUILayout.Popup("External Script Editor", 
                    currentIndex, 
                    installations.Select(installation => installation.Name).ToArray());

                if (selectedIdx != currentIndex) {
                    AirshipExternalCodeEditor.SetCodeEditor(installations[selectedIdx].Path);
                }
                
                if (AirshipExternalCodeEditor.CurrentEditorPath != "")
                    EditorGUILayout.LabelField("Editor Path", AirshipExternalCodeEditor.CurrentEditorPath);
                
                GUILayout.Space(5);
                GUILayout.Label("Debugging", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                {
                    TypescriptLogService.Enabled = EditorGUILayout.ToggleLeft(
                        new GUIContent("Enable Logging", "Enables logging for the Typescript services"), 
                        TypescriptLogService.Enabled
                    );
                    if (TypescriptLogService.Enabled && GUILayout.Button("Open Log File...", GUILayout.Width(150))) {
                        TypescriptLogService.OpenLogFile();
                    }
                    
                    if (TypescriptLogService.Enabled && TypescriptLogService.HasPrevLog && GUILayout.Button("Open Prev Log File...", GUILayout.Width(150))) {
                        TypescriptLogService.OpenPrevLogFile();
                    }
                }
                EditorGUILayout.EndHorizontal();

                
                TypescriptCompilationService.ShowDeveloperOptions = EditorGUILayout.ToggleLeft(
                    new GUIContent("Developer Options (Advanced)", "Enable the advanced options - only enable this if you know what you're doing!"), 
                    TypescriptCompilationService.ShowDeveloperOptions
                );
            }
            AirshipEditorGUI.EndSettingGroup();
            
            if (GUI.changed) {
                localSettings.Modify();
            }
        }

        private static GUIContent projectSettingsIcon;
        private static GUIContent userSettingsIcon;
        
        private static TypescriptOptionsTab selectedTab;
        internal static void RenderSettings(string searchContext) {
            projectSettingsIcon ??= EditorGUIUtility.IconContent("Project");
            userSettingsIcon ??= EditorGUIUtility.IconContent("BuildSettings.Standalone.Small");

            selectedTab = (TypescriptOptionsTab) AirshipEditorGUI.BeginTabs((int) selectedTab, new[] {
                new GUIContent(" Project Settings", projectSettingsIcon.image), 
                new GUIContent(" User Settings", userSettingsIcon.image)
            });
            {
                if (selectedTab == TypescriptOptionsTab.ProjectSettings) {
                    RenderProjectSettings();
                }
                else {
                    RenderLocalSettings();
                }
            }
            AirshipEditorGUI.EndTabs();
        }
    }
}