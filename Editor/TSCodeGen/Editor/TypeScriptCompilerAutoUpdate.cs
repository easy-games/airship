using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            NodePackages.LoadAuthToken();
            
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            
            // Get the remote version of unity-ts
            var remoteVersion = NodePackages.GetCommandOutput(typeScriptDirectories[0], "view @easy-games/unity-ts version")[0];
            var remoteVersionInt = GetBuildVersion(remoteVersion);
            
            foreach (var dir in typeScriptDirectories) {
                var dirPkgInfo = NodePackages.ReadPackageJson(dir);
                
                var toolPackageJson = NodePackages.GetPackageInfo(dir, "@easy-games/unity-ts");
                var toolVersion = GetBuildVersion(toolPackageJson.Version);

                if (toolVersion < remoteVersionInt) {
                    Debug.Log(
                        $"TypeScript compiler for '{dirPkgInfo.Name}' is v{toolVersion}, latest is {remoteVersionInt} - updating to latest!");
                    NodePackages.RunNpmCommand(dir, $"install @easy-games/unity-ts@{remoteVersion}");
                }
            }
        }

        private static int GetBuildVersion(string buildVersionString) {
            return int.Parse(buildVersionString.Split("-")[1]);
        }
    }
}