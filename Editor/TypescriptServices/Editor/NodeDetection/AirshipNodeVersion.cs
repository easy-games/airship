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
        private const string AIRSHIP_NPM_PATH = "airshipNpmPath";
        private const string AIRSHIP_NODE_CUSTOM_PATH = "airshipCustomNodePath";
        
        private static List<IAirshipNodeDistribution> _nodeVersions = new ();
        public static IAirshipNodeDistribution[] available => _nodeVersions.ToArray();
        public static CustomNodeDistribution CustomNodeDistribution { get; } = new();

        public static IAirshipNodeVersion current {
            get {
                var path = EditorPrefs.GetString(AIRSHIP_NODE_PATH);
                return FindNodeInstallByPath(path, out var install) ? install : automaticNodeVersion;
            }
        }

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

        [InitializeOnLoadMethod]
        public static void Init() {
            IAirshipNodeDistribution[] versions = {
#if UNITY_EDITOR_WIN
                new AirshipWindowsNodeDistribution(),
#elif UNITY_EDITOR_OSX
                new AirshipOsxNodeDistribution(),
#elif UNITY_EDITOR_LINUX
                new AirshipLinuxNodeDistribution(),
#endif
                new AirshipNvmNodeDistribution(),
            };

            foreach (var version in versions) {
                if (version.Installs == null || version.Installs.Length == 0) continue;
                _nodeVersions.Add(version);
            }
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

        public static void SetNodeInstall(IAirshipNodeVersion install) {
            EditorPrefs.SetString(AIRSHIP_NODE_PATH, install.nodePath);
            EditorPrefs.SetString(AIRSHIP_NPM_PATH, install.npmPath);
        }
    }

    internal class CustomNodeDistribution : IAirshipNodeDistribution {
        public int priority => -1000;
        
        public class AirshipCustomNodeInstall : IAirshipNodeVersion {
            public string name { get; }
            public string binPath { get; }
            public string nodePath { get; }
            public string npmPath { get; }
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Custom";
        public IAirshipNodeVersion[] Installs { get; set; }
        public bool Valid { get; }
    }

    internal class AirshipNvmNodeDistribution : IAirshipNodeDistribution {
        public int priority => 0;
        
        internal class Install : IAirshipNodeVersion {
            public string name { get; internal set; }
            public string nodePath { get; internal set; }
            public string npmPath { get; internal set; }
            public string binPath { get; internal set; }
            
            public string GetCommand(NodeJsArguments arguments) {
                return nodePath + " " + arguments.GetCommandString();
            }
        }

        public string InstallId => "NVM";
        public IAirshipNodeVersion[] Installs { get; }

        public bool Valid {
            get {
#if UNITY_EDITOR_LINUX
                return true;
#else
                return false;
#endif
            }
        }

        public AirshipNvmNodeDistribution() {
            var installs = new List<Install>();
#if UNITY_EDITOR_WIN
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); // should be /home/USER or %USERPROFILE%
#endif
            
            string[] possibleNvmDirs = {
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
                Path.Join(homeDir, ".nvm"), // /home/USER/.nvm (OSX & Linux default)
                Path.Join(homeDir, ".config", "nvm"), // /home/USER/.config/nvm (bazzite?)
#elif UNITY_EDITOR_WIN
                Path.Join(appData, "nvm"),
#endif
            };

#if UNITY_EDITOR_WIN
            foreach (var nvmDir in possibleNvmDirs) {
                if (!Directory.Exists(nvmDir)) continue;

                foreach (var dir in Directory.GetDirectories(nvmDir)) {
                    var node =  Path.Combine(dir, "node.exe");
                    var npm =  Path.Combine(dir, "npm.cmd");
                
                    installs.Add(new Install() {
                        name = $"NVM",
                        nodePath = node,
                        npmPath = npm,
                        binPath = dir,
                    });
                }
            }
#else
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
#endif
            

            
            Installs = installs.ToArray();
        }
    }
}