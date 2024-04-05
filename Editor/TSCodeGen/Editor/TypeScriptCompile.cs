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
        public static readonly GUIStyle OpenCodeFolderStyle = new GUIStyle("ToolbarButton") {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.ImageAbove,
            fontStyle = FontStyle.Normal,
            fixedWidth = 110,
            fixedHeight = 20,
        };

        public static readonly GUIStyle DeviceMobileStyle;
        public static readonly GUIStyle DevicePCStyle;

        public static readonly GUIStyle CompilerServicesStyle;
        public static readonly GUIStyle CompilerServicesButtonStyle;

        public static readonly GUIStyle serverModeDedicated;
        public static readonly GUIStyle serverModeShared;

        public static Texture2D redBackground;

        static ToolbarStyles()
        {
            CommandButtonStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 130,
                fixedHeight = 20,
            };
            PackagesButtonStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 130,
                fixedHeight = 20,
            };
            LocalCharacterButtonStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 180,
                fixedHeight = 20,
            };

            serverModeDedicated = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 165,
                fixedHeight = 20,
            };
            serverModeShared = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 155,
                fixedHeight = 20,
            };

            DeviceMobileStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 120,
                fixedHeight = 20,
            };
            DevicePCStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Normal,
                fixedWidth = 100,
                fixedHeight = 20,
            };
            
            CompilerServicesStyle = new GUIStyle(EditorStyles.label) {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 10, 0, 0)
            };
            
            CompilerServicesButtonStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 10, 0, 0)
            };

            ServerLabelStyle = new GUIStyle("ToolbarButton") {
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
        private static Texture2D typescriptIcon;
        private static Texture2D typescriptIconOff;

        private const string IconOn = "Packages/gg.easy.airship/Editor/TypescriptOk.png";
        private const string IconOff = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        static CompileTypeScriptButton()
        {

            
            ToolbarExtender.RightToolbarGUI.Add(OnRightToolbarGUI);
            ToolbarExtender.LeftToolbarGUI.Add(OnLeftToolbarGUI);
        }

        private static Texture2D LoadImage(string filepath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filepath);
            return texture;
        }

        private static void OnLeftToolbarGUI() {
            if (Application.isPlaying) return;
            
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

            // if (_compiling) {
            //     GUILayout.Button(new GUIContent("Building...", "Airship scripts are being built..."), ToolbarStyles.CommandButtonStyle);
            // } else {
            //     
            //     if (GUILayout.Button(new GUIContent("Compile Scripts", AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/gg.easy.airship/Editor/LuauIcon.png"), "Compiles all Airship scripts. Compiler output is printed into the Unity Console."), ToolbarStyles.CommandButtonStyle)) {
            //         CompileTypeScript();
            //     }
            // }
            if (GUILayout.Button(new GUIContent("Reveal Scripts", "Opens the folder containing code scripts."), ToolbarStyles.OpenCodeFolderStyle)) {
                EditorUtility.RevealInFinder("Assets/Typescript~");
            }
            if (GUILayout.Button(new GUIContent("Airship Packages", "Opens the Airship Packages window."),
                    ToolbarStyles.PackagesButtonStyle)) {
                AirshipPackagesWindow.ShowWindow();
            }
            
            
            GUILayout.FlexibleSpace();
            
            if (typescriptIcon == null)
                typescriptIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconOn);
            
            if (typescriptIconOff == null)
                typescriptIconOff = AssetDatabase.LoadAssetAtPath<Texture2D>(IconOff);
            
            var compilerCount = TypescriptCompilationService.WatchCount;
            if (compilerCount == 0) {
                GUILayout.Label(
                    new GUIContent($" Compiler Is Inactive", typescriptIconOff, "TypeScript compiler services are disabled"), ToolbarStyles.CompilerServicesStyle);

                if (GUILayout.Button(new GUIContent(" Start Typescript", EditorGUIUtility.Load("PlayButton On") as Texture), ToolbarStyles.CompilerServicesButtonStyle)) {
                    TypescriptCompilationService.StartCompilerServices();
                }
            }
            else {
                GUILayout.Label(
                    new GUIContent(compilerCount > 1 ? $" {compilerCount} Compilers Are Running" : " Compiler Is Running", typescriptIcon, $"Compiler services are running"), 
                    ToolbarStyles.CompilerServicesStyle);
                
                if (GUILayout.Button(new GUIContent(" Stop Typescript", EditorGUIUtility.Load("StopButton") as Texture), ToolbarStyles.CompilerServicesButtonStyle)) {
                    TypescriptCompilationService.StopCompilers();
                }
            }
            
            GUILayout.Space(5);
        }

       


    }
}
