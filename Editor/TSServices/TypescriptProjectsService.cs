using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Airship.Editor.LanguageClient;
using CsToTs.TypeScript;
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

        public static IReadOnlyList<TypescriptProject> Projects { get; private set; } = new List<TypescriptProject>();

        public static int ProblemCount => Projects.Sum(v => v.ProblemItems.Count);

        public static int MaxPackageNameLength { get; private set; }

        internal static Dictionary<string, TypescriptProject> ProjectsByPath { get; private set; } = new();
        
        internal static void ReloadProjects() {
            ProjectsByPath.Clear();
            Projects = TypescriptProject.GetAllProjects();
            foreach (var project in Projects) {
                var package = project.PackageJson;
                if (package == null) continue;
                ProjectsByPath.Add(project.PackageJson.Name, project);
                Debug.Log($"Add ProjectByPath {project.PackageJson.Name} = {project.Directory}");
                
                MaxPackageNameLength = Math.Max(package.Name.Length, MaxPackageNameLength);
            }

            var firstProject = Projects[0];
            var client = new TypescriptLanguageClient(firstProject.Directory);
            var proc = client.Start();
            client.SendRequest(TsServerRequests.ConfigureRequest(new TsServerConfigureArguments() {
                hostInfo = "airship/editor",
                watchOptions = new TsWatchOptions() {
                    watchDirectory = WatchDirectoryKind.UseFsEvents,
                    watchFile = WatchFileKind.UseFsEvents,
                    fallbackPolling = PollingWatchKind.FixedInterval,
                }
            }));
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
                
                if (args.hyperLinkData.TryGetValue("file", out var data)) {
                    OpenFileInEditor(data, line, column);
                }
            };
        }

        public static void OpenFileInEditor(string file, int line = 0, int column = 0) {
            var nonAssetPath = Application.dataPath.Replace("/Assets", "");
            
            var executableArgs = TypescriptEditorArguments.Select(value => Regex.Replace(value, "{([A-z]+)}", 
                (ev) => {
                    var firstMatch = ev.Groups[1].Value;
                    if (firstMatch == "filePath") {
                        return file;
                    } else if (firstMatch == "line") {
                        return line.ToString(CultureInfo.InvariantCulture);
                    } else if (firstMatch == "column") {
                        return column.ToString(CultureInfo.InvariantCulture);
                    }
                            
                    return firstMatch;
                })).ToArray();

            
            if (executableArgs.Length == 0 || executableArgs[0] == "") return;
#if UNITY_EDITOR_OSX
            var startInfo = new ProcessStartInfo("/bin/zsh", string.Join(" ", executableArgs)) {
                CreateNoWindow = true,
                UseShellExecute = true,
                WorkingDirectory = nonAssetPath
            };
#else
            var startInfo = new ProcessStartInfo("cmd.exe", $"/K {string.Join(" ", executableArgs)}") {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = nonAssetPath
            };
#endif
                    

            Process.Start(startInfo);
        }
        
        public static string[] TypescriptEditorArguments {
            get {
                var editorConfig = EditorIntegrationsConfig.instance;
                if (editorConfig.typescriptEditor == TypescriptEditor.VisualStudioCode) {
                    return new[] { "code", "--goto", "{filePath}:{line}:{column}" };
                }
                else {
                    return editorConfig.typescriptEditorCustomPath.Split(' ');
                }
            }
        }



        public static readonly string[] managedPackages = {
            "@easy-games/unity-ts",
            "@easy-games/unity-flamework-transformer",
            "@easy-games/compiler-types"
        };

        private static string[] obsoletePackages = {
            "@easy-games/unity-rojo-resolver",
            "@easy-games/unity-inspect",
            "@easy-games/unity-object-utils"
        };

        internal static Semver MinCompilerVersion => Semver.Parse("3.0.190");
        internal static Semver MinFlameworkVersion => Semver.Parse("1.1.52");
        internal static Semver MinTypesVersion => Semver.Parse("3.0.42");
        
        [MenuItem("Airship/TypeScript/Update Packages")]
        internal static void UpdateTypescript() {
            if (Application.isPlaying) return;

            var watchMode = TypescriptCompilationService.IsWatchModeRunning;
            if (watchMode) {
                TypescriptCompilationService.StopCompilers();
            }

            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            if (typeScriptDirectories.Length <= 0) {
                Debug.LogWarning("Could not find TypeScript directories under project");
                return;
            }

            foreach (var obsoletePackage in obsoletePackages) {
                foreach (var directory in typeScriptDirectories) {
                    var dirPkgInfo = NodePackages.ReadPackageJson(directory);
                    if (dirPkgInfo.DevDependencies.ContainsKey(obsoletePackage)) {
                        Debug.LogWarning($"Has obsolete package {obsoletePackage}");
                        NodePackages.RunNpmCommand(directory, $"uninstall {obsoletePackage}");
                    }
                }
            }
            
            EditorUtility.DisplayProgressBar(TsProjectService, "Checking TypeScript packages...", 0f);

            items = typeScriptDirectories.Length * managedPackages.Length;
            packagesChecked = 0;

            var shouldFullCompile = false;
            foreach (var directory in typeScriptDirectories) {
                if (Directory.Exists(Path.Join(directory, "node_modules"))) continue;
                
                EditorUtility.DisplayProgressBar(TsProjectService, $"Running npm install for {directory}...", 0f);
                
                // Install non-installed package pls
                NodePackages.RunNpmCommand(directory, "install");
                shouldFullCompile = true;
            }
            
            foreach (var managedPackage in managedPackages) {
                EditorUtility.DisplayProgressBar(TsProjectService, $"Checking {managedPackage} for updates...", (float) packagesChecked / items);
                CheckUpdateForPackage(typeScriptDirectories, managedPackage, "staging"); // lol
            }
            EditorUtility.ClearProgressBar();
            
           
            if (shouldFullCompile)
                TypescriptCompilationService.FullRebuild();

            ReloadProjects();
            
            if (watchMode) {
                TypescriptCompilationService.StartCompilerServices();
            }
        }

        private static int items = 0;
        private static int packagesChecked = 0;

        internal static void CheckUpdateForPackage(IReadOnlyList<string> typeScriptDirectories, string package, string tag = "latest") {

            // Get the remote version of unity-ts
            var remoteVersionList = NodePackages.GetCommandOutput(typeScriptDirectories[0], $"view {package}@{tag} version");
            if (remoteVersionList.Count == 0) return;
            var remoteVersion = remoteVersionList[0];

            var remoteSemver = Semver.Parse(remoteVersion);
            
            foreach (var dir in typeScriptDirectories) {
                var dirPkgInfo = NodePackages.ReadPackageJson(dir);
                
                var toolPackageJson = NodePackages.GetPackageInfo(dir, package);
                if (toolPackageJson == null) {
                    Debug.LogWarning($"no package.json for tool {package}");
                    continue;
                }
                
                var toolSemver = Semver.Parse(toolPackageJson.Version);

                if (remoteSemver > toolSemver) {
                    EditorUtility.DisplayProgressBar(TsProjectService, $"Updating {package} in {dir}...", (float) packagesChecked / items);
                    if (NodePackages.RunNpmCommand(dir, $"install {package}@{tag}")) {
                        Debug.Log($"{package} was updated to v{remoteSemver} for {dirPkgInfo.Name}");
                    }
                    else {
                        Debug.Log($"Failed to update {package} to version {remoteSemver}");
                    }
                }

                packagesChecked += 1;
                EditorUtility.DisplayProgressBar(TsProjectService, $"Checked {package} in {dir}...", (float) packagesChecked / items);
            }
            
          
        }
    }
}