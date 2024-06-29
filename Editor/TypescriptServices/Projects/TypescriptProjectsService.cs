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

        internal static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH")!;
            return values.Split(Path.PathSeparator).Select(path => Path.Combine(path, fileName)).FirstOrDefault(File.Exists);
        }

        private static string _codePath;
        public static string VSCodePath => _codePath ??= GetFullPath("code");
        
        public static IReadOnlyList<TypescriptProject> Projects {
            get {
                if (Project != null) {
                    return new[] { Project };
                }

                return new TypescriptProject[] {};
            }
        }

        public static int ProblemCount => Projects.Sum(v => v.ProblemItems.Count);

        public static int MaxPackageNameLength { get; private set; }

        private static TypescriptProject _project;

        [CanBeNull]
        public static TypescriptConfig WorkspaceProjectConfig => Project?.TsConfig;

        internal static Dictionary<string, TypescriptProject> ProjectsByPath { get; private set; } = new();

        /// <summary>
        /// The project for the workspace
        /// </summary>
        public static TypescriptProject Project {
            get {
                if (_project == null) {
                    var projectConfigPath = EditorIntegrationsConfig.instance.typescriptProjectConfig;
                    var directory = Path.GetDirectoryName(projectConfigPath);
                    var file = Path.GetFileName(projectConfigPath);

                    if (TypescriptConfig.FindInDirectory(directory, out var config, file) &&
                        NodePackages.FindPackageJson("Assets/Typescript~", out var package)) {
                        _project = new TypescriptProject(config, package);
                        ProjectsByPath[projectConfigPath] = _project;
                    }
                }

                return _project;
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

        private static string[] obsoletePackages = {
            "@easy-games/unity-rojo-resolver",
            "@easy-games/compiler-types",
            "@easy-games/unity-flamework-transformer"
        };

        internal static Semver MinCompilerVersion => Semver.Parse("3.2.201");

        private static void ValidateTypescriptProject(TypescriptProject project) {
            var tsconfig = project.TsConfig;
            
            if (tsconfig.RemoveTransformer("@easy-games/unity-flamework-transformer")) {
                Debug.LogWarning("Removed deprecated transformer from project");
                tsconfig.Modify();
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

            foreach (var obsoletePackage in obsoletePackages) {
                if (!package.HasDependency(obsoletePackage)) continue;
                package.UninstallDependency(obsoletePackage);
            }

            NodePackages.RunNpmCommand(package.Directory, "install");
            
            var shouldFullCompile = false;
            List<string> managedPackages = new List<string>() { };

            if (managedPackages.Count == 0) {
                EditorUtility.ClearProgressBar();
                return;
            }
            
            items = managedPackages.Count;
            packagesChecked = 0;
            foreach (var managedPackage in managedPackages) {
                EditorUtility.DisplayProgressBar(TsProjectService, $"Checking {managedPackage} for updates...", (float) packagesChecked / items);
                CheckUpdateForPackage(Projects, managedPackage, "staging"); // lol
            }
            EditorUtility.ClearProgressBar();
            
           
            if (shouldFullCompile)
                TypescriptCompilationService.FullRebuild();

            foreach (var project in Projects) {
                ValidateTypescriptProject(project);
            }
            
            ReloadProject();
            
            if (watchMode) {
                TypescriptCompilationService.StartCompilerServices();
            }
        }

        private static int items = 0;
        private static int packagesChecked = 0;

        internal static void CheckUpdateForPackage(IReadOnlyList<TypescriptProject> projects, string package, string tag = "latest") {

            // Get the remote version of unity-ts
            if (!NodePackages.GetCommandOutput(projects[0].Package.Directory, $"view {package}@{tag} version",
                    out var remoteVersionList)) {
                Debug.LogWarning($"Failed to fetch remote version of {package}@{tag} from {projects[0].Name}");
                return;
            }
            if (remoteVersionList.Count == 0) return;
            var remoteVersion = remoteVersionList[^1];
            var remoteSemver = Semver.Parse(remoteVersion);
            
            foreach (var project in projects) {
                var dirPkgInfo = project.Package;

                // Don't overwrite local packages
                if (dirPkgInfo.IsLocalDependency(package)) {
                    Debug.LogWarning($"Skipping local package install of {package}...");
                    continue;
                }

                if (dirPkgInfo.IsGitDependency(package)) {
                    Debug.Log($"{package} was pinned to github");
                    continue;
                }
                else {
                    var toolPackageJson = dirPkgInfo.GetDependencyInfo(package);
                    if (toolPackageJson == null) {
                        Debug.LogWarning($"no package.json for tool {package}");
                        continue;
                    }
                
                    var toolSemver = Semver.Parse(toolPackageJson.Version);

                    if (remoteSemver > toolSemver) {
                        EditorUtility.DisplayProgressBar(TsProjectService, $"Updating {package} in {project.Name}...", (float) packagesChecked / items);
                        if (NodePackages.RunNpmCommand(dirPkgInfo.Directory, $"install {package}@{tag}")) {
                            Debug.Log($"{package} was updated to v{remoteSemver} for {dirPkgInfo.Name}");
                        }
                        else {
                            Debug.Log($"Failed to update {package} to version {remoteSemver}");
                        }
                    }
                }

                packagesChecked += 1;
                EditorUtility.DisplayProgressBar(TsProjectService, $"Checked {package} in {project.Name}...", (float) packagesChecked / items);
            }
        }
    }
}