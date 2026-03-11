#if UNITY_EDITOR_LINUX
using System.Collections.Generic;
using System.IO;

namespace Airship.Editor
{
    internal class AirshipLinuxNodeDistribution : IAirshipNodeDistribution {
        public int priority => 1000;

        public class AirshipLinuxNodeInstall : IAirshipNodeVersion {
            public string name { get; internal set; }
            public string binPath { get; internal set; }
            public string nodePath => Path.Join(binPath, "node");
            public string npmPath => Path.Join(binPath, "npm");
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Node for Linux";
        public IAirshipNodeVersion[] Installs { get; }
        public bool Valid { get; }
        
        public AirshipLinuxNodeDistribution() {
            var installs = new List<IAirshipNodeVersion>();
            
            string[] paths = {
                "/usr/bin", // system binary
                "/usr/local/bin", // local binary
            };

            foreach (var path in paths) {
                var nodePath = Path.Join(path, "node");
                var npmPath = Path.Join(path, "npm");
                
                if (File.Exists(nodePath) && File.Exists(npmPath)) {
                    installs.Add(new AirshipLinuxNodeInstall() {
                        binPath = path,
                        name = "Node for Linux",
                    });
                }
            }

            Installs = installs.ToArray();
        }
    }
}
#endif