using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    public class TypescriptPopupWindow : PopupWindowContent {
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

        public override Vector2 GetWindowSize() {
            var projects = TypescriptProjectsService.Projects;
            var projectCount = projects.Count;
            
            var wsize = base.GetWindowSize();
            return new Vector2(400, 60 + 11 + (11 * projectCount) + 60 * projectCount);
        }

        internal static void RenderProjects() { 
            var projects = TypescriptProjectsService.Projects;
            AirshipEditorGUI.HorizontalLine();
            
            foreach (var project in projects) {
                var packageJson = project.PackageJson;
                if (packageJson == null) continue;
                
                EditorGUILayout.BeginHorizontal(GUILayout.Height(50), GUILayout.Width(380));
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.LabelField(
                            packageJson.Name.StartsWith("@") ? $"{packageJson.Name} (Package)" : packageJson.Name,
                            new GUIStyle(EditorStyles.largeLabel) {
                                fontStyle = FontStyle.Bold,
                            });
                        EditorGUILayout.LabelField(project.Directory);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Separator();
                    EditorGUILayout.BeginVertical();
                    {
                        var servicesState = TypescriptCompilationServicesState.instance;
                        var compilerProcess = servicesState.GetWatchStateForDirectory(project.Directory);
                        if (compilerProcess != null && compilerProcess.IsActive) {
                            if (GUILayout.Button(new GUIContent("Stop Watch Mode",
                                    EditorGUIUtility.Load("d_StopButton") as Texture))) {
                                TypescriptCompilationService.StopCompilers(project);
                            }
                        }
                        else if (project.HasCompiler) {
                            if (GUILayout.Button(new GUIContent("Start Watch Mode",
                                    EditorGUIUtility.Load("d_PlayButton") as Texture))) {
                                TypescriptCompilationService.StartCompilers(project);
                            }
                        }

                        if (GUILayout.Button(new GUIContent("Open Project Folder"))) {
                            EditorUtility.RevealInFinder(Path.Join(project.Directory, "tsconfig.json"));
                        }
                    }
                    EditorGUILayout.Separator();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
                
                AirshipEditorGUI.HorizontalLine();
            }
        }

        public override void OnGUI(Rect rect) {
            EditorGUILayout.LabelField("TypeScript", new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold});

            EditorGUILayout.BeginHorizontal();
            
            var compilerCount = TypescriptCompilationService.WatchCount;
            if (compilerCount > 0) {
                if (GUILayout.Button(
                        new GUIContent(" Stop Typescript", EditorGUIUtility.Load("StopButton") as Texture),
                        MenuItem)) {
                    TypescriptCompilationService.StopCompilers();
                }
            }
            else {
                if (GUILayout.Button(
                        new GUIContent(" Start TypeScript", EditorGUIUtility.Load("PlayButton On") as Texture),
                        MenuItem)) {
                    TypescriptCompilationService.StartCompilerServices();
                }
            }

            if (GUILayout.Button(
                    new GUIContent(" Settings", EditorGUIUtility.Load("d_SettingsIcon") as Texture),
                    MenuItem)) {
                TypescriptOptions.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();

            RenderProjects();
        }
    }
    
    [EditorWindowTitle(title = "TypeScript Projects")]
    public class TypescriptOptions : EditorWindow {
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow(typeof(TypescriptOptions));
            window.Show();
        }
    
        private bool showProjects = true;
        private bool showSettings = true;
        
        private Vector2 scrollPosition;
        private Rect area;
        private void OnGUI() {
           
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
            
            AirshipEditorGUI.HorizontalLine();

            this.showSettings = EditorGUILayout.Foldout(this.showSettings, new GUIContent("Typescript Settings"), true,EditorStyles.foldoutHeader);
            if (this.showSettings) {
                AirshipEditorGUI.HorizontalLine();
                
                var settings = EditorIntegrationsConfig.instance;

                settings.automaticTypeScriptCompilation =
                    EditorGUILayout.ToggleLeft(new GUIContent("Automatically Run on Editor Startup"), settings.automaticTypeScriptCompilation);
                
                AirshipEditorGUI.HorizontalLine();
            }
            
            this.showProjects = EditorGUILayout.Foldout(this.showProjects, new GUIContent("Typescript Projects"), true,EditorStyles.foldoutHeader);
            

            
            if (this.showProjects) {
                var projects = TypescriptProjectsService.Projects;
                AirshipEditorGUI.HorizontalLine();

                this.scrollPosition = EditorGUILayout.BeginScrollView(this.scrollPosition);
                {
                    foreach (var project in projects) {
                        var packageJson = project.PackageJson;

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
                            EditorGUILayout.BeginHorizontal(); 
                            {
                                EditorGUILayout.LabelField("Compiler", project.CompilerVersion.ToString(), EditorStyles.whiteLabel);
         
                                if (GUILayout.Button("Update")) {
                                    foreach (var managedPackage in TypescriptProjectsService.managedPackages) {
                                    TypescriptProjectsService.CheckUpdateForPackage(
                                        new string[] { project.Directory }, managedPackage, "staging");
                                    }
                                }
                            } 
                            EditorGUILayout.EndHorizontal();
                           
                            EditorGUILayout.LabelField("Types", project.CompilerTypesVersion.Revision.ToString(), EditorStyles.whiteLabel);
                            EditorGUILayout.LabelField("Flamework", project.FlameworkVersion.ToString(), EditorStyles.whiteLabel);
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
            }
        }
    }
}