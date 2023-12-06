using System;
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
        private static string _remoteVersion;
        
        static TypeScriptUpdate() {
            // var typeDir = TypeScriptDirFinder.FindTypeScriptDirectory();
            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            
            Debug.Log($"Found TypeScript Directories: {string.Join(", ", typeScriptDirectories)}");

            // Get the remote version of unity-ts
            var remoteVersion = NodePackages.GetCommandOutput(typeScriptDirectories[0], "view @easy-games/unity-ts version")[0];
            _remoteVersion = remoteVersion;
            
            Debug.Log($"Latest Compiler Version is {remoteVersion}");
            
            foreach (var dir in typeScriptDirectories) {
                var toolPackageJson = NodePackages.GetPackageInfo(dir, "@easy-games/unity-ts");
                var toolVersion = toolPackageJson.Version;

                if (toolVersion != remoteVersion) {
                    Debug.LogWarning($"({dir}) {toolVersion} != (remote) {remoteVersion} - Will need update");
                    _outdated = true;
                }
            }
            
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
        }
        
        private static void OnToolbarGUI()
        {
            if (Application.isPlaying) return;

            if (_outdated) {
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button(new GUIContent("Update TS Compiler", "Your TypeScript compiler is out of date. Clicking this will update the version to the remote version"), ToolbarStyles.ServerLabelStyle)) {
                    var typeDir = TypeScriptDirFinder.FindTypeScriptDirectory();
                    NodePackages.LoadAuthToken();
                    NodePackages.RunNpmCommand(typeDir, $"install @easy-games/unity-ts@{_remoteVersion}");
                    _outdated = false;
                }
            }

            
        }
    }
}