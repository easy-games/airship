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

        [MenuItem("Airship/TypeScript/Update Compiler")]
        static void UpdateTypescript() {
            ThreadPool.QueueUserWorkItem(delegate {
                if (Application.isPlaying) return;
                NodePackages.LoadAuthToken();
            
                var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
                if (typeScriptDirectories.Length > 0) {
                    CheckUpdateForPackage(typeScriptDirectories, "@easy-games/unity-ts", "staging");
                    CheckUpdateForPackage(typeScriptDirectories, "@easy-games/compiler-types", "staging");
                }
            });
        }

        private static void CheckUpdateForPackage(string[] typeScriptDirectories, string package, string tag = "latest") {
            // Get the remote version of unity-ts
            var remoteVersionList = NodePackages.GetCommandOutput(typeScriptDirectories[0], $"view {package}@{tag} version");
            if (remoteVersionList.Count == 0) return;
            var remoteVersion = remoteVersionList[0];

            var remoteSemver = GetSemver(remoteVersion);

            var remoteBuildVersion = GetBuildVersion(remoteSemver);
            
            foreach (var dir in typeScriptDirectories) {
                var dirPkgInfo = NodePackages.ReadPackageJson(dir);
                
                var toolPackageJson = NodePackages.GetPackageInfo(dir, package);
                var toolSemver = GetSemver(toolPackageJson.Version);
                var toolBuildVersion = GetBuildVersion(toolSemver);

                Debug.Log($"Remote version is {remoteSemver.VersionInt}@{remoteBuildVersion}");
                if ((remoteSemver.VersionInt == toolSemver.VersionInt && toolBuildVersion < remoteBuildVersion) || remoteSemver.VersionInt > toolSemver.VersionInt) {
                    Debug.Log(
                        $"{package} for '{dirPkgInfo.Name}' is v{toolBuildVersion}, latest is {remoteBuildVersion} - updating to latest!");
                    NodePackages.RunNpmCommand(dir, $"install {package}@{tag}");
                }
            }
        }

        private struct Semver {
            public int Major { get; set; }
            public int Minor { get; set; }

            public int Revision { get; set; }
            public string Prerelease { get; set; }

            public long VersionInt => Major * 1_000_000 + Minor * 1_000 + Revision;
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