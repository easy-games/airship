using System.IO;
using System.Linq;
using CsToTs.TypeScript;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;

namespace Airship.Editor {
    [InitializeOnLoad]
    public class TypeScriptUpdate {
        private static bool _outdated = false;
        
        static TypeScriptUpdate() {
            var typeDir = TypeScriptDirFinder.FindTypeScriptDirectory();
            
            // Get the remote version of unity-ts
            var version = NodePackages.GetCommandOutput(typeDir, "view @easy-games/unity-ts version")[0];

            // Get the locally installed version of unity-ts
            var toolPackageJson = NodePackages.GetPackageInfo(typeDir, "@easy-games/unity-ts");
            var toolVersion = toolPackageJson.Version;
            
            // Get the package.json version
            var packageJson = NodePackages.ReadPackageJson(typeDir);
            var packageVersion = packageJson.DevDependencies["@easy-games/unity-ts"].Substring(1);
            

            if (toolVersion != packageVersion) {
                Debug.LogWarning($"Locally installed version of TypeScript compiler mismatch: (package.json) != {toolVersion} (bin)");
                _outdated = true;
            }

            if (toolVersion != version) {
                Debug.LogWarning($"Latest TypeScript compiler version is {version} - you have {toolVersion} installed."); // lol
                
                NodePackages.LoadAuthToken();
                NodePackages.RunNpmCommand(typeDir, $"install @easy-games/unity-ts@{version}");
            }
        }
    }
}