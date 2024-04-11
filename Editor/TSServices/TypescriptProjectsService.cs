using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CsToTs.TypeScript;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;

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

        private static IReadOnlyList<TypescriptProject> projects;
        public static IReadOnlyList<TypescriptProject> Projects => projects; // ??
        public static int MaxPackageNameLength { get; private set; }

        public static bool HasCoreTypes {
            get {
                return Directory.Exists("Assets/Bundles/Types~/@Easy/Core");
            }
        }
        
        internal static void ReloadProjects() {
            projects = TypescriptProject.GetAllProjects();
            foreach (var project in projects) {
                var package = project.PackageJson;
                if (package == null) continue;
                MaxPackageNameLength = Math.Max(package.Name.Length, MaxPackageNameLength);
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