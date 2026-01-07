#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;

namespace Airship.Editor
{
    internal class AirshipWindowsNodeDistribution : IAirshipNodeDistribution {
        public int priority => 1000;

        public class AirshipWindowsNodeInstall : IAirshipNodeVersion {
            public string name { get; internal set; }
            public string binPath { get; internal set; }
            public string nodePath => Path.Join(binPath, "node.exe");
            public string npmPath => Path.Join(binPath, "npm.cmd");
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Node for Windows";
        public IAirshipNodeVersion[] Installs { get; }
        public bool Valid { get; }

        public AirshipWindowsNodeDistribution() {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFilesX64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            
            string[] possibleNodeDirs = {
                Path.Join(programFilesX86, "nodejs"),
                Path.Join(programFilesX64, "nodejs"),
            };

            List<IAirshipNodeVersion> installs = new List<IAirshipNodeVersion>();
            foreach (var nvmDir in possibleNodeDirs) {
                if (!Directory.Exists(nvmDir)) continue;
                
                var nodeExe = Path.Join(nvmDir, "node.exe");
                var npmCmd = Path.Join(nvmDir, "npm.cmd");
                if (File.Exists(nodeExe) && File.Exists(npmCmd)) {
                    installs.Add(new AirshipWindowsNodeInstall() {
                        name = "Node for Windows",
                        binPath = nvmDir,
                    });
                }
            }

            Installs = installs.ToArray();
        }
    }
}
#endif