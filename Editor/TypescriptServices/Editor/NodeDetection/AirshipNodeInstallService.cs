using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal interface IAirshipNodeVersion {
        public string name { get; }
        public string binPath { get; }
        public string nodePath { get; }
        public string npmPath { get; }
        public string GetCommand(NodeJsArguments arguments);
    }
    internal interface IAirshipNodeDistribution {
        public int priority { get; }
        public string InstallId { get; }
        public IAirshipNodeVersion[] Installs { get; }
        public bool Valid { get; }
    }

    internal static class AirshipNodeInstallService {
        private const string AIRSHIP_NODE_PATH = "airshipNodePath";
        private const string AIRSHIP_NODE_VERSION = "airshipNodeVersion";
        private const string AIRSHIP_NPM_PATH = "airshipNpmPath";
        private const string AIRSHIP_NODE_CUSTOM_PATH = "airshipCustomNodePath";
        
        private static List<IAirshipNodeDistribution> _nodeVersions = new ();
        public static IAirshipNodeDistribution[] available => _nodeVersions.ToArray();
        public static CustomNodeDistribution custom { get; } = new();
        
        public static IAirshipNodeVersion current {
            get {
                var id = EditorPrefs.GetString(AIRSHIP_NODE_VERSION);
                if (!string.IsNullOrEmpty(id)) {
                    return FindNodeVersionByName(id, out var install) ? install : automaticNodeVersion;
                } else {
                    var path = EditorPrefs.GetString(AIRSHIP_NODE_PATH);
                    return FindNodeInstallByPath(path, out var install) ? install : automaticNodeVersion;
                }
            }
            internal set => EditorPrefs.SetString(AIRSHIP_NODE_VERSION, value.name);
        }

        public static bool hasNodeInstall => current != null;

        public static IAirshipNodeVersion automaticNodeVersion {
            get {
                if (available.Length == 0) return null;

                IAirshipNodeDistribution distribution = null;
                foreach (var nodeVersion in available) {
                    if (distribution is { Installs: not null } && nodeVersion.priority < distribution.priority) continue;
                    distribution = nodeVersion;
                }

                return distribution is { Installs: { Length: > 0 } } ? distribution.Installs[0] : null;
            }
        }

        public static IAirshipNodeVersion[] GetVersions() {
            var nodeInstalls =  new List<IAirshipNodeVersion>();
            
            foreach (var nodeVersion in available) {
                if (nodeVersion.Installs == null) continue;
                nodeInstalls.AddRange(nodeVersion.Installs);
            }
            
            return nodeInstalls.ToArray();
        }
        
        [InitializeOnLoadMethod]
        public static void Init() {
            ScanNodeVersions();
        }

        [MenuItem("Airship/TypeScript/Reload Node Installs...", priority = 0)]
        public static void ScanNodeVersions() {
            _nodeVersions.Clear();
            IAirshipNodeDistribution[] versions = {
#if UNITY_EDITOR_WIN
                new AirshipWindowsNodeDistribution(),
#elif UNITY_EDITOR_OSX
                new AirshipOsxNodeDistribution(),
#elif UNITY_EDITOR_LINUX
                new AirshipLinuxNodeDistribution(),
#endif
                new AirshipNvmNodeDistribution(),
#if UNITY_EDITOR_LINUX
                custom,
#endif
            };

            foreach (var version in versions) {
                if (version.Installs == null || version.Installs.Length == 0) continue;
                _nodeVersions.Add(version);
            }
        }

        public static bool FindNodeVersionByName(string id, out IAirshipNodeVersion install) {
            if (string.IsNullOrEmpty(id)) {
                install = null;
                return false;
            }
            
            foreach (var version in available) {
                foreach (var versionInstall in version.Installs) {
                    if (versionInstall.name != id) continue;
                    install = versionInstall;
                    return true;
                }
            }
            
            install = null;
            return false;
        }

        public static bool FindNodeInstallByPath(string nodePath, out IAirshipNodeVersion install) {
            if (nodePath == "") {
                install = null;
                return false;
            }

            if (available.Length == 0) {
                install = null;
                return false;
            }

            foreach (var version in available) {
                foreach (var versionInstall in version.Installs) {
                    if (versionInstall.nodePath != nodePath) continue;
                    install = versionInstall;
                    return true;
                }
            }

            install = null;
            return false;
        }

        public static bool IsValidNodeDirectory(string dir) {
            if (!Directory.Exists(dir)) {
                return false;
            }
            
#if UNITY_EDITOR_WIN
            var nodePath = Path.Join(dir, "node.exe");
            var npmPath = Path.Join(dir, "npm.cmd");
#else
            var nodePath = Path.Join(dir, "node");
            var npmPath = Path.Join(dir, "npm");
#endif
            return File.Exists(nodePath) && File.Exists(npmPath);
        }

        public static void SetNodeInstall(IAirshipNodeVersion install) {
            EditorPrefs.SetString(AIRSHIP_NODE_PATH, install.nodePath);
            EditorPrefs.SetString(AIRSHIP_NPM_PATH, install.npmPath);
            current = install;
        }
    }
}