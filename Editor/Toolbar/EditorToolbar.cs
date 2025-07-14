using System;
using System.Reflection;
using System.Threading.Tasks;
using Code.Authentication;
using Code.Http.Internal;
using Editor.Packages;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;
using PopupWindow = UnityEditor.PopupWindow;

namespace Airship.Editor
{
    class PostProcessHook : AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {
            Profiler.BeginSample("RepaintEditorToolbar");
            AirshipToolbar.RepaintToolbar();
            Profiler.EndSample();
        }
    }
    
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

        static ToolbarStyles() {
            var red = Texture2D.redTexture;
            
            CommandButtonStyle = new GUIStyle("ToolbarButton") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = FontStyle.Normal,
                // fixedWidth = 130,
                
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
            
            CompilerServicesButtonStyle = new GUIStyle("TE ToolbarDropDown") {
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
    public static class AirshipToolbar
    {
        private static Texture2D typescriptIcon;
        private static Texture2D typescriptIconDev;
        private static Texture2D typescriptIconErr;
        public static Texture2D typescriptIconOff;
        public static Texture2D gamePublish;
        public static Texture2D gameSettings;
        public static Texture2D coreUpdateTexture;
        
        private static Material profilePicRounded;
        private static Texture signedOutIcon;
        private static Texture2D signedInIcon;
        public static byte[] signedInIconBytes = new byte[]{};
        /** True when fetching game info for publish (publish shouldn't be clickable during this) */
        private static bool fetchingPublishInfo;
        
        private const string IconOn = "Packages/gg.easy.airship/Editor/TypescriptOk.png";
        private const string IconOnLight = "Packages/gg.easy.airship/Editor/TypescriptOkLight.png";
        private const string IconDev = "Packages/gg.easy.airship/Editor/TypescriptDev.png";
        private const string IconErr = "Packages/gg.easy.airship/Editor/TypescriptErr.png";
        private const string IconOff = "Packages/gg.easy.airship/Editor/TypescriptOff.png";
        private const string IconSettings = "Packages/gg.easy.airship/Editor/gear-outline.png";
        private const string IconPublish = "Packages/gg.easy.airship/Editor/upload-solid.png";
        private const string SignedOutIcon = "Packages/gg.easy.airship/Editor/GrayProfilePicture.png";
        private const string coreUpdateIcon = "Packages/gg.easy.airship/Editor/CoreUpdate.png";
        
        static AirshipToolbar()
        {
            RunCore.launchInDedicatedServerMode = EditorPrefs.GetBool("AirshipDedicatedServerMode", false);
            ToolbarExtender.RightToolbarGUI.Add(OnRightToolbarGUI);
            ToolbarExtender.LeftToolbarGUI.Add(OnLeftToolbarGUI);

            if (EditorAuthManager.localUser != null || !string.IsNullOrEmpty(InternalHttpManager.editorUserId)) {
                // This is delayed because we're running in InitializeOnLoad but FetchAndUpdateSignedInIcon
                // requires AssetDatabase to be loaded.
                EditorApplication.delayCall += FetchAndUpdateSignedInIcon;
            }
            EditorAuthManager.localUserChanged += (user) => {
                if (EditorAuthManager.signInStatus != EditorAuthSignInStatus.SIGNED_IN) {
                    signedInIconBytes = new byte[]{};
                    RepaintToolbar();
                    return;
                }
                FetchAndUpdateSignedInIcon();
            };

            // Delete "Assets/EditorIcons.asset"
            if (AssetDatabase.AssetPathExists("Assets/EditorIcons.asset")) {
                AssetDatabase.DeleteAsset("Assets/EditorIcons.asset");
            }
        }
        
        private static void FetchAndUpdateSignedInIcon() {
            EditorAuthManager.DownloadProfilePicture().ContinueWith((t) => {
                if (t.Result == null) return;

                signedInIcon = ResizeTexture(t.Result, 128, 128);
                AirshipToolbar.signedInIconBytes = signedInIcon.EncodeToPNG();
                // EditorIcons.Instance.signedInIcon = signedInIcon.EncodeToPNG();
                // EditorUtility.SetDirty(EditorIcons.Instance);
                // AssetDatabase.SaveAssetIfDirty(EditorIcons.Instance);
                RepaintToolbar();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static void RepaintToolbar() {
            if (ToolbarCallback.m_currentToolbar == null) return;
            
            var root = ToolbarCallback.m_currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            var rawRoot = root.GetValue(ToolbarCallback.m_currentToolbar);
            var mRoot = rawRoot as VisualElement;
            mRoot.MarkDirtyRepaint();
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
                        : "Server Mode: Shared", "Dedicated (default): client and server are run from different windows (requires MPPM or ParrelSync)\n\nShared (advanced): both client and server run from the same window. This means the client is acting as a server host (peer-to-peer). Both RunUtil.IsServer() and RunUtil.IsClient() will return true."),
                    RunCore.launchInDedicatedServerMode ? ToolbarStyles.serverModeDedicated : ToolbarStyles.serverModeShared)) {
                RunCore.launchInDedicatedServerMode = !RunCore.launchInDedicatedServerMode;
                EditorPrefs.SetBool("AirshipDedicatedServerMode", RunCore.launchInDedicatedServerMode);
            }
        }
        
        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight) {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;
            
            if (profilePicRounded == null)
                profilePicRounded = AssetDatabase.LoadAssetAtPath<Material>("Packages/gg.easy.airship/Editor/Hidden_EditorProfilePicRounded.mat");
            
            RenderTexture.active = rt;
            Graphics.Blit(source, rt, profilePicRounded);

            Texture2D result = new Texture2D(targetWidth, targetHeight);
            result.filterMode = FilterMode.Bilinear;
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private static Rect buttonRect;
        private static Rect profileButtonRect;
        private static void OnRightToolbarGUI()
        {
            if (Application.isPlaying) return;

            if (ClonesManager.IsClone()) {
                GUILayout.Button(new GUIContent("Server Window | Read Only!", "Do not make changes to the project in this window. Instead, use the main client editor window."), ToolbarStyles.ServerLabelStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            if (gamePublish == null)
                gamePublish = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPublish);
            if (gameSettings == null)
                gameSettings = AssetDatabase.LoadAssetAtPath<Texture2D>(IconSettings);
            if (signedOutIcon == null)
                signedOutIcon = ResizeTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(SignedOutIcon), 128, 128);
            if (signedInIcon == null && AirshipToolbar.signedInIconBytes != null && AirshipToolbar.signedInIconBytes.Length > 0) {
                Texture2D result = new Texture2D(128, 128);
                result.filterMode = FilterMode.Bilinear;
                result.LoadImage(AirshipToolbar.signedInIconBytes);
                result.Apply();
                signedInIcon = result;
            }

            if (coreUpdateTexture == null) {
                coreUpdateTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(coreUpdateIcon);
            }
            
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.toolbarButton);

            EditorGUIUtility.SetIconSize(new Vector2(14, 14));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            var signedIn = EditorAuthManager.signInStatus == EditorAuthSignInStatus.SIGNED_IN;
            var deployKey = AuthConfig.instance.deployKey;
            var publishButtonEnabled = signedIn || !string.IsNullOrEmpty(deployKey) || fetchingPublishInfo;
            
            EditorGUIUtility.SetIconSize(new Vector2(18, 16)); // Because image is weirdly shaped...
            var pressedSettings = GUILayout.Button(new GUIContent() {
                image = gameSettings,
                tooltip = "Airship project settings"
            }, buttonStyle);
            if (pressedSettings) {
                GameConfigEditor.FocusGameConfig();
            }
            
            if (!publishButtonEnabled) {
                GUI.enabled = false;
            }

            EditorGUIUtility.SetIconSize(new Vector2(14, 14));
            var pressedPublish = GUILayout.Button(new GUIContent() {
                image = gamePublish,
                text = " Publish",
                tooltip = "Publish your game to Airship"
            }, buttonStyle);
            if (pressedPublish) {
                Deploy.PromptPublish();
            }
            GUI.enabled = true;

            EditorGUIUtility.SetIconSize(new Vector2(16, 16));
            Texture profileIcon = signedInIcon;
            if (profileIcon == null || EditorAuthManager.signInStatus == EditorAuthSignInStatus.SIGNED_OUT) {
                profileIcon = signedOutIcon;
            }
            var profileButtonClicked = GUILayout.Button(new GUIContent(profileIcon), buttonStyle);
            if (Event.current.type == EventType.Repaint) profileButtonRect = GUILayoutUtility.GetLastRect();
            if (profileButtonClicked) {
                GenericMenu menu = new GenericMenu();
                
                if (signedIn) {
                    var nameTxt = "";
                    if (EditorAuthManager.localUser != null) {
                        nameTxt = " " + EditorAuthManager.localUser.username;
                    }

                    menu.AddItem(new GUIContent("Sign out" + nameTxt), false,
                        () => { EditorAuthManager.Logout(); });
                } else {
                    menu.AddItem(new GUIContent("Sign in with Google"), false,
                        () => { AuthManager.AuthWithGoogle(); });
                }

                menu.DropDown(profileButtonRect);
            }
            GUILayout.EndHorizontal();

            if (AirshipPackageAutoUpdater.isCoreUpdateAvailable) {
                GUILayout.Space(20);
                GUIStyle coreUpdateStyle = new GUIStyle(EditorStyles.toolbarButton);
                EditorGUIUtility.SetIconSize(new Vector2(14, 14));
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUIUtility.SetIconSize(new Vector2(14, 14));
                if (
                    GUILayout.Button(new GUIContent() {
                        image = coreUpdateTexture,
                        text = " Core Update Available!",
                        tooltip = "A new version of Airship Core is available. It's recommended to update immediately.",
                    }, coreUpdateStyle)
                ) {
                    AirshipPackageAutoUpdater.isCoreUpdateAvailable = false;
                    RepaintToolbar();
                    EditorCoroutines.Execute(AirshipPackageAutoUpdater.CheckAllPackages(GameConfig.Load(), false, true));
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            
            if (typescriptIcon == null)
                typescriptIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorGUIUtility.isProSkin ? IconOn : IconOnLight);

            if (typescriptIconErr == null)
                typescriptIconErr = AssetDatabase.LoadAssetAtPath<Texture2D>(IconErr);
            
            if (typescriptIconOff == null)
                typescriptIconOff = AssetDatabase.LoadAssetAtPath<Texture2D>(IconOff);
            
            if (typescriptIconDev == null)
                typescriptIconDev = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDev);

            var isSmallScreen = Screen.width < 1920;

            var errorCount = TypescriptCompilationService.ErrorCount;

            var project = TypescriptProjectsService.Project;
            if (project != null) {
                var compilerText = "";
                if (errorCount > 0) {
                    if (isSmallScreen) {
                        compilerText =
                            $" {TypescriptCompilationService.ErrorCount} {(TypescriptCompilationService.ErrorCount == 1 ? "Error" : " Errors")}";
                    }
                    else {
                        compilerText =
                            $" {TypescriptCompilationService.ErrorCount} Compilation {(TypescriptCompilationService.ErrorCount == 1 ? "Error" : " Errors")}";
                    }
                } 
                else {
                    compilerText = TypescriptCompilationService.ShowDeveloperOptions && !TypescriptCompilationService.Crashed ? $" TypeScript" : "";
                }


                
                var isDev = false;
                if (TypescriptCompilationService.CompilerVersion ==
                    TypescriptCompilerVersion.UseLocalDevelopmentBuild) {
                    compilerText += " Development Build";
                    isDev = true;
                }

                var compilerName = TypescriptCompilationService.CompilerVersion switch {
                    TypescriptCompilerVersion.UseEditorVersion => "Typescript Compiler",
                    TypescriptCompilerVersion.UseLocalDevelopmentBuild => "Development Typescript Compiler",
                    _ => ""
                };

                var tooltip = TypescriptCompilationService.IsWatchModeRunning switch {
                    true => $"{compilerName}: Running",
                    false => $"Using the {compilerName}",
                };

                if (!TypescriptServices.IsCompilerActive && !TypescriptServices.IsAwaitingRestart) {
                    compilerText = " TypeScript [Inactive]";
                    tooltip = "The typescript compiler is not running!";
                }
                
                var style = new GUIStyle(ToolbarStyles.CompilerServicesButtonStyle);
                if (project.HasCrashed && !TypescriptCompilationService.IsWatchModeRunning) {
                    compilerText = " Typescript <CRASHED>";
                    style.normal.textColor = new Color(1, 0.8f, 0.4f);
                    style.fontStyle = FontStyle.Bold;
                }

                if (!TypescriptCompilationService.IsWatchModeRunning) {
                    style.normal.textColor = new Color(1, 0.5f, 0.5f);
                    style.fontStyle = FontStyle.Bold;
                    
                    EditorGUILayout.LabelField(new GUIContent("", "TypeScript is not running!"), new GUIStyle("CN EntryWarnIconSmall"), GUILayout.Width(20));
                }

                //if (TypescriptCompilationService.ShowTypescriptDropdown) {
                var typescriptCompilerDropdown = EditorGUILayout.DropdownButton(
                    new GUIContent(
                        Screen.width < 1366 ? "" : compilerText, 
                        TypescriptCompilationService.ErrorCount > 0 ? typescriptIconErr : TypescriptCompilationService.IsWatchModeRunning ? (isDev ? typescriptIconDev : typescriptIcon) : typescriptIconOff, 
                        tooltip),
                    FocusType.Keyboard,
                    style);
            
                if (typescriptCompilerDropdown) {
                    var wind = new TypescriptPopupWindow();
                    PopupWindow.Show(buttonRect, wind);
                }
                if (Event.current.type == EventType.Repaint) buttonRect = GUILayoutUtility.GetLastRect();
                //}
            }
            
            GUILayout.Space(5);
        }
    }
}
