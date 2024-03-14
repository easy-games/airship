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
    [InitializeOnLoad]
    public class TypeScriptCompilerAutoUpdate {
        static TypeScriptCompilerAutoUpdate() {
            // Since we need to be online to check the version + update TS
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                return;
            }
            
            UpdateTypescript();
        }

        private static string[] managedPackages = {
            "@easy-games/unity-ts",
            "@easy-games/unity-flamework-transformer",
            "@easy-games/compiler-types"
        };

        [MenuItem("Airship/TypeScript/Update Packages")]
        static void UpdateTypescript() {
            if (Application.isPlaying) return;
            ThreadPool.QueueUserWorkItem(delegate {
                var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
                if (typeScriptDirectories.Length <= 0) return;
                
                foreach (var managedPackage in managedPackages) {
                    CheckUpdateForPackage(typeScriptDirectories, managedPackage, "staging");
                }
            });
        }

        private static void CheckUpdateForPackage(IReadOnlyList<string> typeScriptDirectories, string package, string tag = "latest") {
            // Get the remote version of unity-ts
            var remoteVersionList = NodePackages.GetCommandOutput(typeScriptDirectories[0], $"view {package}@{tag} version");
            if (remoteVersionList.Count == 0) return;
            var remoteVersion = remoteVersionList[0];

            var remoteSemver = GetSemver(remoteVersion);
            
            foreach (var dir in typeScriptDirectories) {
                var dirPkgInfo = NodePackages.ReadPackageJson(dir);
                
                var toolPackageJson = NodePackages.GetPackageInfo(dir, package);
                var toolSemver = GetSemver(toolPackageJson.Version);
                
                if (remoteSemver > toolSemver && NodePackages.RunNpmCommand(dir, $"install {package}@{remoteSemver}")) {
                    Debug.Log($"{package} was updated to v{remoteSemver} for {dirPkgInfo.Name}");
                }
            }
        }

        private struct Semver {
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

            public override string ToString() {
                return Prerelease != null ? $"{Major}.{Minor}.{Revision}-{Prerelease}" : $"{Major}.{Minor}.{Revision}";
            }
        }

        private static Semver GetSemver(string versionString) {
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
        
        private static int GetBuildVersion(Semver version) {
            if (int.TryParse(version.Prerelease, out var buildVersion)) {
                return buildVersion;
            }
            else {
                return 0;
            }
        }
    }
}