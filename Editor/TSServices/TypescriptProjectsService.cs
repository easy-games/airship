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
    [InitializeOnLoad]
    public static class TypescriptProjectsService {
        private static IReadOnlyList<TypescriptProject> projects;
        public static IReadOnlyList<TypescriptProject> Projects => projects; // ??
        public static int MaxPackageNameLength { get; private set; }
        
        static TypescriptProjectsService() {
            // Since we need to be online to check the version + update TS
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                return;
            }

            ReloadProjects();
            UpdateTypescript();
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

        [MenuItem("Airship/TypeScript/Update Packages")]
        internal static void UpdateTypescript() {
            if (Application.isPlaying) return;

            var watchMode = TypescriptCompilationService.IsWatchModeRunning;
            if (watchMode) {
                TypescriptCompilationService.StopCompilers();
            }
            
            ThreadPool.QueueUserWorkItem(delegate {
                var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
                if (typeScriptDirectories.Length <= 0) return;

                foreach (var obsoletePackage in obsoletePackages) {
                    foreach (var directory in typeScriptDirectories) {
                        var dirPkgInfo = NodePackages.ReadPackageJson(directory);
                        if (dirPkgInfo.DevDependencies.ContainsKey(obsoletePackage)) {
                            Debug.LogWarning($"Has obsolete package {obsoletePackage}");
                            NodePackages.RunNpmCommand(directory, $"uninstall {obsoletePackage}");
                        }
                    }
                }
                
                foreach (var managedPackage in managedPackages) {
                    CheckUpdateForPackage(typeScriptDirectories, managedPackage, "staging");
                }

                if (watchMode) {
                    TypescriptCompilationService.StartCompilerServices();
                }
            });
        }

        internal static void CheckUpdateForPackage(IReadOnlyList<string> typeScriptDirectories, string package, string tag = "latest") {
            // Get the remote version of unity-ts
            var remoteVersionList = NodePackages.GetCommandOutput(typeScriptDirectories[0], $"view {package}@{tag} version");
            if (remoteVersionList.Count == 0) return;
            var remoteVersion = remoteVersionList[0];

            var remoteSemver = Semver.Parse(remoteVersion);
            
            foreach (var dir in typeScriptDirectories) {
                var dirPkgInfo = NodePackages.ReadPackageJson(dir);
                
                var toolPackageJson = NodePackages.GetPackageInfo(dir, package);
                var toolSemver = Semver.Parse(toolPackageJson.Version);

                if (remoteSemver > toolSemver) {
                    if (NodePackages.RunNpmCommand(dir, $"install {package}@{tag}")) {
                        Debug.Log($"{package} was updated to v{remoteSemver} for {dirPkgInfo.Name}");
                    }
                    else {
                        Debug.Log($"Failed to update {package} to version {remoteSemver}");
                    }
                }
            }
        }
    }
}