using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Editor;
using JetBrains.Annotations;
using Unity.CodeEditor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    public interface IAirshipExternalCodeEditor {
        public string EditorId { get; }
        CodeEditor.Installation[] Installations { get; }
        bool OpenProject(string filePath = "", int line = -1, int column = -1);
    }
    
    [InitializeOnLoad]
    public static class AirshipExternalCodeEditor {
        private const string AIRSHIP_EXTERNAL_EDITOR = "airshipExternalEditor";
        
        private static readonly IAirshipExternalCodeEditor defaultEditor = new AirshipSystemCodeExternalEditor();
        private static IAirshipExternalCodeEditor currentEditor;
        private static string currentEditorPath = EditorPrefs.GetString(AIRSHIP_EXTERNAL_EDITOR);
        internal static List<IAirshipExternalCodeEditor> Editors { get; } = new();
        public static string CurrentEditorPath => currentEditorPath;
        public static IAirshipExternalCodeEditor CurrentEditor {
            get {
                if (FindEditorByPath(currentEditorPath, out var editor)) {
                    return editor;
                }
                
                var vscode = Editors.Find(f => f.EditorId == "vscode");
                var installs = vscode?.Installations;
                if (installs is { Length: > 0 }) {
                    SetCodeEditor(installs[0].Path);
                    currentEditor = vscode;
                }
                
                return currentEditor;
            }
            internal set {
                if (value.Installations.Length > 0) {
                    SetCodeEditor(value.Installations[0].Path);
                }
                else {
                    SetCodeEditor("");
                }
            }
        }

        static AirshipExternalCodeEditor() {
            RegisterExternalEditor(new AirshipVisualStudioCodeExternalEditor());
            currentEditor = defaultEditor;
        }

        private static bool FindEditorByPath(string editorPath, out IAirshipExternalCodeEditor editor) {
            if (editorPath == "") {
                editor = defaultEditor;
                return true;
            }
            
            if (Editors.Count == 0) {
                editor = null;
                return false;
            }

            foreach (var item in Editors) {
                if (item.Installations.Length == 0) continue;
                if (item.Installations.All(installation => installation.Path != editorPath)) continue;
                
                editor = item;
                return true;
            }

            editor = null;
            return false;
        }

        internal static void SetCodeEditor(string editorPath) {
            if (editorPath == null) return;
            if (!FindEditorByPath(editorPath, out var editor)) return;
            
            EditorPrefs.SetString(AIRSHIP_EXTERNAL_EDITOR, editorPath);
            currentEditor = editor;
            currentEditorPath = editorPath;
            Debug.Log("External Typescript Editor set to " + editorPath);
        }

        static IAirshipExternalCodeEditor RegisterExternalEditor(IAirshipExternalCodeEditor editor) {
            Editors.Add(editor);
            return editor;
        }
    }

    public class AirshipSystemCodeExternalEditor : IAirshipExternalCodeEditor {
        public string EditorId => "system";
        public CodeEditor.Installation[] Installations { get; } = { };
        
        public bool OpenProject(string filePath = "", int line = -1, int column = -1) {
            var relPath = Path.GetRelativePath(Application.dataPath, filePath);
            var absPath = Path.GetFullPath(relPath, Application.dataPath);
            
            Application.OpenURL("file:///" + absPath);
            return true;
        }
    }

    public class AirshipVisualStudioCodeExternalEditor : IAirshipExternalCodeEditor {
        public string EditorId => "vscode";
        public CodeEditor.Installation[] Installations { get; }

        public AirshipVisualStudioCodeExternalEditor() {
            List<CodeEditor.Installation> installations = new();

            string[] visualStudioCodePaths;
            string[] vscodeInsidersPaths;
            
#if UNITY_EDITOR_WIN
            visualStudioCodePaths = new[] {
                Path.Join(Environment.GetEnvironmentVariable("LOCALAPPDATA"), 
                    @"Programs\Microsoft VS Code\bin\code.cmd")
            };
            vscodeInsidersPaths = new[] {
                Path.Join(Environment.GetEnvironmentVariable("LOCALAPPDATA"), 
                    @"Programs\Microsoft VS Code Insiders\bin\code.cmd")
            };
#elif UNITY_EDITOR_OSX
            visualStudioCodePaths = new[] {
                "/usr/local/bin/code",
                "/Applications/Visual Studio Code.app/Contents/MacOS/Electron"
            };
            vscodeInsidersPaths = new[] {
                "/usr/local/bin/code-insiders",
                "/Applications/Visual Studio Code Insiders.app/Contents/MacOS/Electron"
            };
#else
            visualStudioCodePaths = new string[] { };
            vscodeInsidersPaths = new string[] { };
#endif
            foreach (var possiblePath in visualStudioCodePaths) {
                if (!File.Exists(possiblePath)) continue;
                installations.Add(new CodeEditor.Installation() {
                    Name = "Visual Studio Code",
                    Path = possiblePath
                });
                break;
            }
            
            foreach (var possiblePath in vscodeInsidersPaths) {
                if (!File.Exists(possiblePath)) continue;
                installations.Add(new CodeEditor.Installation() {
                    Name = "Visual Studio Code (Insiders)",
                    Path = possiblePath
                });
                break;
            }
            Installations = installations.ToArray();
        }
        
        public bool OpenProject(string filePath = "", int line = 0, int column = 0) {
            if (Installations.Length == 0) {
                return false;
            }

            var path = Installations[0].Path;
            if (path.Contains(" ")) {
                path = $"\"{path}\"";
            }
            
            List<string> args = new List<string>( new [] {
                path,
            });

            if (filePath != "") {
                args.Add("--goto");
                args.Add($"{filePath}:{line}:{column}");
            }
            else {
                args.Add(".");
            }

            var processStartInfo = ShellProcess.GetShellStartInfoForCommand(string.Join(" ", args), Application.dataPath); 
            Process.Start(processStartInfo);
            Debug.Log($"{processStartInfo.FileName} {processStartInfo.Arguments}");
            return true;
        }
    }
}