using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Code.GameBundle;
using CsToTs.TypeScript;
using Editor.Packages;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;

namespace Airship.Editor
{
    internal static class ToolbarStyles
    {
        public static readonly GUIStyle CommandButtonStyle;
        public static readonly GUIStyle CommandButtonDisabledStyle;
        public static readonly GUIStyle PackagesButtonStyle;
        public static readonly GUIStyle LocalCharacterButtonStyle;
        public static readonly GUIStyle ServerLabelStyle;
        public static readonly GUIStyle OpenCodeFolderStyle = new GUIStyle("Command") {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.ImageAbove,
            fontStyle = FontStyle.Normal,
            fixedWidth = 110,
            fixedHeight = 20,
        };

        public static readonly GUIStyle DeviceMobileStyle;
        public static readonly GUIStyle DevicePCStyle;

        public static readonly GUIStyle serverModeDedicated;
        public static readonly GUIStyle serverModeShared;

        public static Texture2D redBackground;

        static ToolbarStyles()
        {
            CommandButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 130,
                fixedHeight = 20,
            };
            PackagesButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 130,
                fixedHeight = 20,
            };
            LocalCharacterButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 180,
                fixedHeight = 20,
            };

            serverModeDedicated = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 165,
                fixedHeight = 20,
            };
            serverModeShared = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 155,
                fixedHeight = 20,
            };

            DeviceMobileStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 120,
                fixedHeight = 20,
            };
            DevicePCStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 100,
                fixedHeight = 20,
            };

            ServerLabelStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold,
                fixedWidth = 200,
                fixedHeight = 20,
            };
            redBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            redBackground.SetPixel(0, 0, new Color(0.3f, 0f, 0f, 1f));
            redBackground.Apply(); // not sure if this is necessary
            ServerLabelStyle.normal.background = redBackground;
        }
    }

    [InitializeOnLoad]
    public static class CompileTypeScriptButton
    {
        private static bool _compiling;
        private static readonly GUIContent BuildButtonContent;
        private static readonly GUIContent CompileInProgressContent;


        private const string BuildIcon = "Packages/gg.easy.airship/Editor/TSCodeGen/Editor/build-ts.png";

        [MenuItem("Airship/Full Script Rebuild")]
        public static void FullRebuild()
        {
            CompileTypeScript(true);
        }

        static CompileTypeScriptButton()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnRightToolbarGUI);
            ToolbarExtender.LeftToolbarGUI.Add(OnLeftToolbarGUI);

            BuildButtonContent = new GUIContent
            {
                text = "  Build Game",
                image = LoadImage(BuildIcon),
            };
            CompileInProgressContent = new GUIContent
            {
                text = "  Building...",
                image = LoadImage(BuildIcon),
            };
        }

        private static Texture2D LoadImage(string filepath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filepath);
            return texture;
        }

        private static void OnLeftToolbarGUI() {
            if (Application.isPlaying) return;

            GUILayout.Label("Runinng (2) TypeScript projects?");
            
            GUILayout.FlexibleSpace();
            bool simulateMobile = SessionState.GetBool("AirshipSimulateMobile", false);
            if (GUILayout.Button(new GUIContent(simulateMobile
                        ? "Device: Mobile"
                        : "Device: PC", ""),
                    simulateMobile ? ToolbarStyles.DeviceMobileStyle : ToolbarStyles.DevicePCStyle)) {
                SessionState.SetBool("AirshipSimulateMobile", !simulateMobile);
            }
            if (GUILayout.Button(new GUIContent(RunCore.launchInDedicatedServerMode
                        ? "Server Mode: Dedicated"
                        : "Server Mode: Shared", "Shared (default): both client and server run from the same window. This means the client is acting as a server host (peer-to-peer). Both RunUtil.IsServer() and RunUtil.IsClient() will return true. \n\nDedicated: client and server are run from different windows (requires MPPM or ParrelSync)"),
                    RunCore.launchInDedicatedServerMode ? ToolbarStyles.serverModeDedicated : ToolbarStyles.serverModeShared)) {
                RunCore.launchInDedicatedServerMode = !RunCore.launchInDedicatedServerMode;
                SessionState.SetBool("AirshipDedicatedServerMode", RunCore.launchInDedicatedServerMode);
            }
        }

        private static void OnRightToolbarGUI()
        {
            if (Application.isPlaying) return;

            if (ClonesManager.IsClone()) {
                GUILayout.Button(new GUIContent("Server Window | Read Only!", "Do not make changes to the project in this window. Instead, use the main client editor window."), ToolbarStyles.ServerLabelStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            if (_compiling) {
                GUILayout.Button(new GUIContent("Building...", "Airship scripts are being built..."), ToolbarStyles.CommandButtonStyle);
            } else {
                
                if (GUILayout.Button(new GUIContent("Compile Scripts", AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/gg.easy.airship/Editor/LuauIcon.png"), "Compiles all Airship scripts. Compiler output is printed into the Unity Console."), ToolbarStyles.CommandButtonStyle)) {
                    CompileTypeScript();
                }
            }
            if (GUILayout.Button(new GUIContent("Reveal Scripts", "Opens the folder containing code scripts."), ToolbarStyles.OpenCodeFolderStyle)) {
                EditorUtility.RevealInFinder("Assets/Typescript~");
            }
            if (GUILayout.Button(new GUIContent("Airship Packages", "Opens the Airship Packages window."),
                    ToolbarStyles.PackagesButtonStyle)) {
                AirshipPackagesWindow.ShowWindow();
            }
        }

        private static void CompileTypeScriptProject(string packageDir, bool shouldClean = false) {
            var packageInfo = NodePackages.ReadPackageJson(packageDir);
            Debug.Log($"Running compilation for project {packageInfo.Name}");
            
            var outPath = Path.Join(packageDir, "out");
            if (shouldClean && Directory.Exists(outPath))
            {
                Debug.Log("Deleting out folder..");
                Directory.Delete(outPath, true);
            }
            
            try
            {
                _compiling = true;
                        
                Debug.Log("Installing NPM dependencies...");
                var success = RunNpmInstall(packageDir);
                if (!success)
                {
                    Debug.LogWarning("Failed to install NPM dependencies");
                    _compiling = false;
                    return;
                }

                var successfulBuild = RunNpmBuild(packageDir);
                _compiling = false;
                if (successfulBuild)
                {
                    Debug.Log($"<color=#77f777><b>Successfully built '{packageInfo.Name}'</b></color>");
                }
                else
                {
                    Debug.LogWarning($"<color=red><b>Failed to build'{packageInfo.Name}'</b></color>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        private static void CompileTypeScript(bool shouldClean = false) {
            var packages = GameConfig.Load().packages;
            
            Dictionary<string, string> localPackageTypescriptPaths = new();
            List<string> typescriptPaths = new();
            
            // @Easy/Core has the highest priority for internal dev
            var compilingCorePackage = false;
            
            NodePackages.LoadAuthToken();
            
            // Fetch all 
            foreach (var package in packages)
            {
                // Compile local packages first
                if (!package.localSource) continue;
                var tsPath = TypeScriptDirFinder.FindTypeScriptDirectoryByPackage(package);
                if (tsPath == null) {
                    Debug.LogWarning($"{package.id} is declared as a local package, but has no TypeScript code?");
                    continue;
                }

                localPackageTypescriptPaths.Add(package.id, tsPath);
            }
            
            
            // Grab any non-package TS dirs
            var packageDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var packageDir in packageDirectories) {
                if (localPackageTypescriptPaths.ContainsValue(packageDir)) continue;
                typescriptPaths.Add(packageDir);
            }

            // Force @Easy/Core to front
            // If core package exists, then we force it to be compiled first
            if (localPackageTypescriptPaths.ContainsKey("@Easy/Core")) {
                var corePkgDir = localPackageTypescriptPaths["@Easy/Core"];
                localPackageTypescriptPaths.Remove("@Easy/Core");
                
                compilingCorePackage = true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    CompileTypeScriptProject(corePkgDir, shouldClean);
                    compilingCorePackage = false;
                });
            }

            var compilingLocalPackage = false;
            // Compile each additional local package
            foreach (var packageDir in localPackageTypescriptPaths.Values) {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // Wait for the other local packages
                    while (compilingCorePackage || compilingLocalPackage) Thread.Sleep(1000);
                    compilingLocalPackage = true;
                    CompileTypeScriptProject(packageDir, shouldClean);
                    compilingLocalPackage = false;
                });
            }
            
            // Compile the non package TS dirs
            foreach (var packageDir in typescriptPaths) {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // If we're compiling core, wait for that...
                    while (compilingCorePackage || compilingLocalPackage) Thread.Sleep(1000);
                    CompileTypeScriptProject(packageDir, shouldClean);
                });
            }
        }

        private static bool RunNpmInstall(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "install");
        }

        private static bool RunNpmBuild(string dir)
        {
            return NodePackages.RunNpmCommand(dir, "run build");
        }


    }
}
