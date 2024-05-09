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
            
            CompilerServicesButtonStyle = new GUIStyle("ToolbarDropdown") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 20, 0, 0)
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
        private static Texture2D typescriptIconErr;
        public static Texture2D typescriptIconOff;

        private const string IconOn = "Packages/gg.easy.airship/Editor/TypescriptOk.png";
        private const string IconErr = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        private const string IconOff = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        static CompileTypeScriptButton()
        {
            RunCore.launchInDedicatedServerMode = EditorPrefs.GetBool("AirshipDedicatedServerMode", false);
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

            if (GUILayout.Button(new GUIContent(RunCore.launchInDedicatedServerMode
                        ? "Server Mode: Dedicated"
                        : "Server Mode: Shared", "Shared (default): both client and server run from the same window. This means the client is acting as a server host (peer-to-peer). Both RunUtil.IsServer() and RunUtil.IsClient() will return true. \n\nDedicated: client and server are run from different windows (requires MPPM or ParrelSync)"),
                    RunCore.launchInDedicatedServerMode ? ToolbarStyles.serverModeDedicated : ToolbarStyles.serverModeShared)) {
                RunCore.launchInDedicatedServerMode = !RunCore.launchInDedicatedServerMode;
                EditorPrefs.SetBool("AirshipDedicatedServerMode", RunCore.launchInDedicatedServerMode);
            }
        }



        private static Rect buttonRect;
        private static void OnRightToolbarGUI()
        {
            if (Application.isPlaying) return;

            if (ClonesManager.IsClone()) {
                GUILayout.Button(new GUIContent("Server Window | Read Only!", "Do not make changes to the project in this window. Instead, use the main client editor window."), ToolbarStyles.ServerLabelStyle);
                GUILayout.FlexibleSpace();
                return;
            }
            
            if (GUILayout.Button(new GUIContent("" +
                                                "Reveal Scripts", "Opens the folder containing code scripts."), ToolbarStyles.OpenCodeFolderStyle)) {
                EditorUtility.RevealInFinder("Assets/Typescript~");
            }
            if (GUILayout.Button(new GUIContent("Airship Packages", "Opens the Airship Packages window."),
                    ToolbarStyles.PackagesButtonStyle)) {
                AirshipPackagesWindow.ShowWindow();
            }
            
            
            GUILayout.FlexibleSpace();
            
            if (typescriptIcon == null)
                typescriptIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconOn);

            if (typescriptIconErr == null)
                typescriptIconErr = AssetDatabase.LoadAssetAtPath<Texture2D>(IconErr);
            
            if (typescriptIconOff == null)
                typescriptIconOff = AssetDatabase.LoadAssetAtPath<Texture2D>(IconOff);

            var compilerCount = TypescriptCompilationService.WatchCount;

            var isSmallScreen = Screen.width < 1920;
            var compilerText = "";

            var errorCount = TypescriptCompilationService.ErrorCount;
            var projectCount = TypescriptProjectsService.Projects.Count;

            if (errorCount > 0) {
                if (isSmallScreen) {
                    compilerText =
                        $" {TypescriptCompilationService.ErrorCount} {(TypescriptCompilationService.ErrorCount == 1 ? "Error" : " Errors")}";
                }
                else {
                    compilerText =
                        $" {TypescriptCompilationService.ErrorCount} Compilation {(TypescriptCompilationService.ErrorCount == 1 ? "Error" : " Errors")}";
                }
            } else if (compilerCount > 0) {
                if (isSmallScreen) {
                    compilerText = " TypeScript";
                }
                else {
                    compilerText = compilerCount > 1 ? $" Typescript ({compilerCount} projects)" : " Typescript";
                }
            }
            else {
                compilerText = " Typescript";
            }

            var typescriptCompilerDropdown = EditorGUILayout.DropdownButton(
                new GUIContent(Screen.width < 1366 ? TypescriptCompilationService.ErrorCount > 0 ? $" {TypescriptCompilationService.ErrorCount}" : "" : compilerText, TypescriptCompilationService.ErrorCount > 0 ? typescriptIconErr : compilerCount > 0 ? typescriptIcon : typescriptIconOff),
                FocusType.Keyboard,
                ToolbarStyles.CompilerServicesButtonStyle);
            
            if (typescriptCompilerDropdown) {
                var wind = new TypescriptPopupWindow();
                PopupWindow.Show(buttonRect, wind);
            }
            if (Event.current.type == EventType.Repaint) buttonRect = GUILayoutUtility.GetLastRect();
            
            GUILayout.Space(5);
        }
    }
}
