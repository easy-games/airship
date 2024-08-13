using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsToTs.TypeScript;
using Editor;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    public struct Semver {
        public int Major { get; set; }
        public int Minor { get; set; }

        public int Revision { get; set; }
        public string Prerelease { get; set; }

        public bool IsNewerThan(Semver other) {
            if (Major > other.Major) {
                return true;
            }

            if (Major == other.Major && Minor > other.Minor) {
                return true;
            }

            return Major == other.Major && Minor == other.Minor && Revision > other.Revision;
        }
            
        public static bool operator >(Semver a, Semver b) {
            return a.IsNewerThan(b);
        }
            
        public static bool operator <(Semver a, Semver b) {
            return b.IsNewerThan(a);
        }

        public static Semver Parse(string versionString) {
            string buildInfo = null;
            
            var components = versionString.Split("-");
            if (components.Length > 1) {
                buildInfo = components[1];
            }
            
            var versionComponents = components[0].Split(".");
            var major = int.Parse(versionComponents[0]);
            var minor = int.Parse(versionComponents[1]);
            var revision = int.Parse(versionComponents[2]);

            return new Semver() {
                Major = major,
                Minor = minor,
                Revision = revision,
                Prerelease = buildInfo
            };
        }
        
        public override string ToString() {
            return Prerelease != null ? $"{Major}.{Minor}.{Revision}-{Prerelease}" : $"{Major}.{Minor}.{Revision}";
        }
    }

    /// <summary>
    /// Services relating to typescript projects
    /// </summary>
    public static class TypescriptProjectsService {
        private const string TsProjectService = "Typescript Project Service";

        private static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH")!;
            return values.Split(Path.PathSeparator).Select(path => Path.Combine(path, fileName)).FirstOrDefault(File.Exists);
        }

        private static string _codePath;
        private static string VSCodePath => _codePath ??= GetFullPath("code");
        
        internal static IEnumerable<TypescriptProject> Projects {
            get {
                return Project != null ? new[] { Project } : new TypescriptProject[] {};
            }
        }

        public static int ProblemCount => Project?.ProblemItems.Count ?? 0;

        private static TypescriptProject _project;
        
        /// <summary>
        /// The project for the workspace
        /// </summary>
        public static TypescriptProject Project {
            get {
                if (_project != null) return _project;
                
                var projectConfigPath = EditorIntegrationsConfig.instance.typescriptProjectConfig;
                var directory = Path.GetDirectoryName(projectConfigPath);
                var file = Path.GetFileName(projectConfigPath);

                if (TypescriptConfig.FindInDirectory(directory, out var config, file) &&
                    NodePackages.FindPackageJson("Assets/Typescript~", out var package)) {
                    _project = new TypescriptProject(config, package);
                }

                return _project;
            }
        }
        
        internal static void HandleRenameEvent(string oldFileName, string newFileName) {
            var components = Resources.FindObjectsOfTypeAll<AirshipComponent>();
            foreach (var component in components) {
                if (component.TypescriptFilePath != oldFileName) continue;
                
                Debug.LogWarning($"File was renamed, changed reference of {oldFileName} to {newFileName} in {component.name}");
                component.SetScriptFromPath(newFileName, component.context);
                EditorUtility.SetDirty(component);
            }
        }

        internal static TypescriptProject ReloadProject() {
            _project = null;
            return Project;
        }
        
        [Obsolete("Use 'ReloadProject'")]
        internal static void ReloadProjects() {
            ReloadProject();
        }
        
        [InitializeOnLoadMethod]
        public static void OnLoad() {
            EditorGUI.hyperLinkClicked += (window, args) => {
                args.hyperLinkData.TryGetValue("line", out var lineString);
                args.hyperLinkData.TryGetValue("col", out var colString);
                
                var line = 0;
                var column = 0;
                if (lineString != null && colString != null && colString != "" && lineString != "") {
                    line = int.Parse(lineString);
                    column = int.Parse(colString);
                }

                if (!args.hyperLinkData.TryGetValue("file", out var data)) return;
                
                if (data.StartsWith("out://") && Project != null) {
                    data = data.Replace("out://", Project.TsConfig.OutDir + "/");
                }
                    
                OpenFileInEditor(data, line, column);
            };
        }

        public static void OpenFileInEditor(string file, int line = 0, int column = 0) {
            AirshipExternalCodeEditor.CurrentEditor.OpenProject(file, line, column);
        }

        public static string[] EditorArguments {
            get {
                var editorConfig = EditorIntegrationsConfig.instance;
                switch (editorConfig.typescriptEditor) {
                    case TypescriptEditor.VisualStudioCode when VSCodePath != null:
                        return new[] { "code", "--goto", "{filePath}:{line}:{column}" };
                    case TypescriptEditor.Custom:
                        return editorConfig.typescriptEditorCustomPath.Split(' ');
                    default:
#if UNITY_EDITOR_OSX
                        return new[] { "open", "{filePath}" };
#else
                        return new[] { "start", "{filePath}" };
#endif
                }
            }
        }
        
        internal static void CheckTypescriptProject() {
            var watchMode = TypescriptCompilationService.IsWatchModeRunning;
            if (watchMode) {
                TypescriptCompilationService.StopCompilers();
            }
            
            // Ensure we've loaded the project
            ReloadProject();
            
            if (Project == null) {
                return;
            }
            
            var package = Project.Package;
            NodePackages.RunNpmCommand(package.Directory, "install", false);
        }
    }
}