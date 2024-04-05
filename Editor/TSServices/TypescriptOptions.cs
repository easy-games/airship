using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    public class PopupWindowContent2 : PopupWindowContent {
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
                
            
            var wsize = base.GetWindowSize();
            return new Vector2(400, 50 + 70 * projects.Count); // 50 + 15
        }

        public override void OnGUI(Rect rect) {
            EditorGUILayout.LabelField("TypeScript Options", new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold});
            
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

            // if (GUILayout.Button(
            //         new GUIContent("Settings", EditorGUIUtility.Load("SettingsIcon") as Texture),
            //         MenuItem)) {
            //     TypescriptOptions.ShowWindow();
            // }
            
            var projects = TypescriptProjectsService.Projects;
               
                
            AirshipEditorGUI.HorizontalLine();

            foreach (var project in projects) {
                var packageJson = project.PackageJson;

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.LabelField(
                        packageJson.Name.StartsWith("@") ? $"{packageJson.Name} (Package)" : packageJson.Name,
                        new GUIStyle(EditorStyles.largeLabel) {
                            fontStyle = FontStyle.Bold
                        });
                    EditorGUILayout.LabelField(project.Directory);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {
                    var servicesState = TypescriptCompilationServicesState.instance;
                    var compilerProcess = servicesState.GetWatchStateForDirectory(project.Directory);
                    if (compilerProcess != null && compilerProcess.IsActive) {
                        GUILayout.Button(new GUIContent("Stop Watch Mode",
                            EditorGUIUtility.Load("d_StopButton") as Texture));
                    }
                    else {
                        GUILayout.Button(new GUIContent("Start Watch Mode",
                            EditorGUIUtility.Load("d_PlayButton") as Texture));
                    }

                    if (GUILayout.Button(new GUIContent("Open Project Folder"))) {
                        EditorUtility.RevealInFinder(Path.Join(project.Directory, "tsconfig.json"));
                    }
                }
                EditorGUILayout.EndVertical();



                EditorGUILayout.EndHorizontal();
                AirshipEditorGUI.HorizontalLine();
            }
        }
    }
    
    // [EditorWindowTitle(title = "TypeScript")]
    // public class TypescriptOptions : EditorWindow {
    //     public static void ShowWindow() {
    //         var window = EditorWindow.GetWindow(typeof(TypescriptOptions));
    //         window.Show();
    //       //  EditorwIn
    //     }
    //
    //     private bool showProjects = true;
    //     private Rect area;
    //     private void OnGUI() {
    //         var servicesState = TypescriptCompilationServicesState.instance;
    //         
    //
    //     }
    // }
}