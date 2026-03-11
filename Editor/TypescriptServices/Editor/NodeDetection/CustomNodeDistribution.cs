using System.IO;
using UnityEditor;

namespace Airship.Editor
{
    
    internal class CustomNodeDistribution : IAirshipNodeDistribution {
        public int priority => -1000;
        
        public class Install : IAirshipNodeVersion {
            private const string AIRSHIP_CUSTOM_NODE_PATH = "airshipCustomNodePath";

            public string name => "Custom";

            public string binPath {
                get => EditorPrefs.GetString(AIRSHIP_CUSTOM_NODE_PATH);
                set => EditorPrefs.SetString(AIRSHIP_CUSTOM_NODE_PATH, value);
            }
            public string nodePath => Path.Join(binPath, "node");
            public string npmPath => Path.Join(binPath, "npm");
            
            public string GetCommand(NodeJsArguments arguments) {
                return "";
            }
        }
        
        public string InstallId => "Custom";
        public IAirshipNodeVersion[] Installs { get; set; }
        public bool Valid { get; }

        public Install CustomInstall;

        public CustomNodeDistribution() {
            CustomInstall = new Install();
            Installs = new IAirshipNodeVersion[] {
                CustomInstall,
            };
        }
    }
}