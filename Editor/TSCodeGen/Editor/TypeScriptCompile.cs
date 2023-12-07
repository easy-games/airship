using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
        public static readonly GUIStyle ServerLabelStyle;
        public static Texture2D redBackground;

        static ToolbarStyles()
        {
            CommandButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold,
                fixedWidth = 130,
                fixedHeight = 20,
            };
            PackagesButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold,
                fixedWidth = 130,
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

        [MenuItem("Airship/♻️ Full Script Rebuild", priority = 202)]
        public static void FullRebuild()
        {
            var tsDir = TypeScriptDirFinder.FindCoreTypeScriptDirectory();
            if (tsDir == null)
            {
                UnityEngine.Debug.LogError("No Typescript~ directory found");
                return;
            }

            var outPath = Path.Join(tsDir, "out");
            if (Directory.Exists(outPath))
            {
                Debug.Log("Deleting out folder...");
                Directory.Delete(outPath, true);
            }
            CompileTypeScript();
        }

        static CompileTypeScriptButton()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);

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

        private static void OnToolbarGUI()
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
                if (GUILayout.Button(new GUIContent("Compile Scripts", "Compiles all Airship scripts. Compiler output is printed into the Unity Console."), ToolbarStyles.CommandButtonStyle)) {
                    CompileTypeScript();
                }
            }
            if (GUILayout.Button(new GUIContent("Airship Packages", "Opens the Airship Packages window."),
                    ToolbarStyles.PackagesButtonStyle)) {
                EditorWindow.GetWindow<AirshipPackagesWindow>(false, "Airship Packages", true);
            }
            GUILayout.FlexibleSpace();
        }

        private static void CompileTypeScriptProject(string packageDir) {
            var packageInfo = NodePackages.ReadPackageJson(packageDir);
            Debug.Log($"Running compilation for project {packageInfo.Name}");
            
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
        
        private static void CompileTypeScript() {
            List<string> packagesToCompile = new();
            
            // @Easy/Core has the highest priority for internal dev
            var corePkgDir = TypeScriptDirFinder.FindCoreTypeScriptDirectory();
            var corePkgCompiling = false;
            
            // Grab the user directories
            var packageDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var packageDir in packageDirectories) {
                if (packageDir == corePkgDir) continue; // exclude core if detected
                packagesToCompile.Add(packageDir);
            }
            
            NodePackages.LoadAuthToken();

            // If core package exists, and it's defo a typescript package then we force it to be compiled first
            if (corePkgDir != null && packageDirectories.Contains(corePkgDir)) {
                // We use corePkgCompiling later on to yield the other compiler threads until it's done
                corePkgCompiling = true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    CompileTypeScriptProject(corePkgDir);
                    corePkgCompiling = false;
                });
            }

            // ForEach user package - compile it
            foreach (var packageDir in packagesToCompile) {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // If we're compiling core, wait for that...
                    while (corePkgCompiling) Thread.Sleep(1000);
                    CompileTypeScriptProject(packageDir);
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
