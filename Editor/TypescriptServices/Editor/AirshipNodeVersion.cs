using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Airship.Editor {
    internal interface IAirshipNodeInstall {
        public string name { get; }
        public string binPath { get; }
        public string nodePath { get; }
        public string npmPath { get; }
        public string GetCommand(NodeJsArguments arguments);
    }
    internal interface IAirshipNodeVersion {
        public string InstallId { get; }
        public IAirshipNodeInstall[] Installs { get; }
        public bool Valid { get; }
    }

    internal static class AirshipNodeVersionService {
        private const string AIRSHIP_NODE_PATH = "airshipNodePath";
        private const string AIRSHIP_NPM_PATH = "airshipNpmPath";
        private const string AIRSHIP_NODE_CUSTOM_PATH = "airshipCustomNodePath";
        
        private static List<IAirshipNodeVersion> _nodeVersions = new ();
        public static IAirshipNodeVersion[] nodeVersions => _nodeVersions.ToArray();
        public static CustomNodeVersion customNodeVersion { get; } = new();

        public static IAirshipNodeInstall currentNodeVersion {
            get {
                var path = EditorPrefs.GetString(AIRSHIP_NODE_PATH);
                return FindNodeInstallByPath(path, out var install) ? install : null;
            }
        }

        [InitializeOnLoadMethod]
        public static void Init() {
#if UNITY_EDITOR_WIN
            var windowsNode = new AirshipWindowsNodeVersion();
            if (windowsNode.Installs.Length > 0) {
                _nodeVersions.Add(windowsNode);
            }
#endif
            
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
            var nvmManagedNode = new AirshipNvmNodeVersion();
            if (nvmManagedNode.Installs.Length > 0) {
                _nodeVersions.Add(nvmManagedNode);
            }
#endif
            _nodeVersions.Add(customNodeVersion);
        }

        public static bool FindNodeInstallByPath(string nodePath, out IAirshipNodeInstall install) {
            if (nodePath == "") {
                install = null;
                return false;
            }

            if (nodeVersions.Length == 0) {
                install = null;
                return false;
            }

            foreach (var version in nodeVersions) {
                foreach (var versionInstall in version.Installs) {
                    if (versionInstall.nodePath != nodePath) continue;
                    install = versionInstall;
                    return true;
                }
            }

            install = null;
            return false;
        }

        public static void SetNodeInstall(IAirshipNodeInstall install) {
            EditorPrefs.SetString(AIRSHIP_NODE_PATH, install.nodePath);
            EditorPrefs.SetString(AIRSHIP_NPM_PATH, install.npmPath);
        }
    }

    internal class AirshipWindowsNodeVersion : IAirshipNodeVersion {
        public string InstallId => "node.exe";
        public IAirshipNodeInstall[] Installs { get; }
        public bool Valid { get; }
    }

    internal class CustomNodeVersion : IAirshipNodeVersion {
        public class AirshipCustomNodeInstall : IAirshipNodeInstall {
            public string name { get; }
            public string binPath { get; }
            public string nodePath { get; }
            public string npmPath { get; }
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Custom";
        public IAirshipNodeInstall[] Installs { get; set; }
        public bool Valid { get; }
    }

    internal class AirshipNvmNodeVersion : IAirshipNodeVersion {
        internal class Install : IAirshipNodeInstall {
            public string name { get; internal set; }
            public string nodePath { get; internal set; }
            public string npmPath { get; internal set; }
            public string binPath { get; internal set; }
            
            public string GetCommand(NodeJsArguments arguments) {
                return nodePath + " " + arguments.GetCommandString();
            }
        }

        public string InstallId => "NVM";
        public IAirshipNodeInstall[] Installs { get; }

        public bool Valid {
            get {
#if UNITY_EDITOR_LINUX
                return true;
#else
                return false;
#endif
            }
        }

        public AirshipNvmNodeVersion() {
            var installs = new List<Install>();
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); // should be /home/USER
 
            string[] possibleNvmDirs = {
#if UNITY_EDITOR_LINUX
                Path.Join(homeDir, ".nvm"), // /home/USER/.nvm (OSX & Linux default)
                Path.Join(homeDir, ".config", "nvm"), // /home/USER/.config/nvm (bazzite?)
#endif
            };

            foreach (var nvmDir in possibleNvmDirs) {
                if (!Directory.Exists(nvmDir)) continue;
                
                var versions = Path.Join(nvmDir, "versions", "node");
                foreach (var dir in Directory.GetDirectories(versions)) {
                    var node =  Path.Combine(dir, "bin", "node");
                    var npm =  Path.Combine(dir, "bin", "npm");
                
                    installs.Add(new Install() {
                        name = $"nvm ({Path.GetFileName(dir)})",
                        nodePath = node,
                        npmPath = npm,
                        binPath = Path.Join(dir, "bin"),
                    });
                }
            }
            
            Installs = installs.ToArray();
        }
    }
}