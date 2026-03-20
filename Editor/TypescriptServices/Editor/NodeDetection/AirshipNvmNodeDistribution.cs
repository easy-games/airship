using System;
using System.Collections.Generic;
using System.IO;

namespace Airship.Editor
{
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
                if (!Directory.Exists(versions)) continue;
                
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