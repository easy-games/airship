#if UNITY_EDITOR_OSX
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airship.Editor
{
    internal class AirshipOsxNodeDistribution : IAirshipNodeDistribution {
        public int priority => 1000;

        public class AirshipOsxNodeInstall : IAirshipNodeVersion {
            public string name { get; internal set; }
            public string binPath { get; internal set; }
            public string nodePath => Path.Join(binPath, "node");
            public string npmPath => Path.Join(binPath, "npm");
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Node for MacOS";
        public IAirshipNodeVersion[] Installs { get; }
        public bool Valid { get; }


        public AirshipOsxNodeDistribution() {
            var installs = new List<IAirshipNodeVersion>();
            
            string[] paths = {
                "/usr/local/bin"
            };

            foreach (var path in paths) {
                var nodePath = Path.Join(path, "node");
                var npmPath = Path.Join(path, "npm");
                
                if (File.Exists(nodePath) && File.Exists(npmPath)) {
                    installs.Add(new AirshipOsxNodeInstall() {
                        binPath = path,
                        name = "node.js",
                    });
                }
            }

            Installs = installs.ToArray();
        }
    }
}
#endif