using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Airship.Editor {
    public enum TypescriptStatusTab {
        Problems,
    }    
    
    [EditorWindowTitle]
    public class TypescriptStatusWindow : EditorWindow {
        [MenuItem("Airship/TypeScriptStatusWin")]
        public static void Open() {
            var window = GetWindow(typeof(TypescriptStatusWindow));
            window.titleContent = new GUIContent("Typescript Services", CompileTypeScriptButton.typescriptIconOff);
            window.Show();
        }

        internal TypescriptStatusTab ActiveTab { get; set; } = TypescriptStatusTab.Problems;

        private void Update() {
            this.titleContent = new GUIContent("Typescript Services", CompileTypeScriptButton.typescriptIconOff);
        }

        private void CreateGUI() {
            throw new NotImplementedException();
        }

        private ListView problemItemListView = new ListView();

            
        
        private void OnEnable() {

        }

        private void OnGUI() {
            GUILayout.BeginHorizontal("Toolbar");
            GUILayout.Label("5", "CN CountBadge");
            GUILayout.Toggle(ActiveTab == TypescriptStatusTab.Problems, "Problems", "ToolbarButtonLeft"); 
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.BeginScrollView(new Vector2());
            {
                foreach (var project in TypescriptProjectsService.Projects) {
                    if (project.ProblemItems == null) continue;
                    foreach (var problemItem in project.ProblemItems) {
                        EditorGUILayout.BeginHorizontal();
                        
                        GUILayout.Button(new GUIContent(problemItem.Message, EditorGUIUtility.Load("console.erroricon") as Texture), new GUIStyle("CN EntryInfo"));
                        
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}