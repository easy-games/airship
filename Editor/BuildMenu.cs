#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_EDITOR_OSX
using UnityEditor.OSXStandalone;
#endif

namespace Editor {
    public class BuildMenu {
        private const string ClientExecutableName = "airship";
        private const string ServerExecutableName = "StandaloneLinux64";
        
        public static string[] scenes = {
            "Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/Login.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/Disconnected.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/AirshipUpdateApp.unity",
        };

        private static string FormatBytes(BuildSummary summary) {
            var bytes = summary.totalSize;
            if (bytes < 1024) {
                return $"{bytes} bytes";
            }
            if (bytes < 1024 * 1024) {
                var kb = bytes / 1024.0f;
                return $"{kb:F2} KB [{bytes} bytes]";
            }
            if (bytes < 1024 * 1024 * 1024) {
                var mb = bytes / (float)(1024 * 1024);
                return $"{mb:F2} MB [{bytes} bytes]";
            }

            var gb = bytes / (float)(1024 * 1024 * 1024);
            return $"{gb:F2} GB [{bytes} bytes]";
        }

        private static void OnBuild() {
            PhysicsSetup.Setup(null);
        }

        public static void BuildLinuxServerStaging() {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Server, new string[] {"AIRSHIP_STAGING", "AIRSHIP_PLAYER", "AIRSHIP_INTERNAL"});
            BuildLinuxServer();
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Server/Linux", priority = 80)]
#endif
        public static void BuildLinuxServer() {
            OnBuild();
            EditorBuildSettingsScene[] scenes = {
                new("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true),
                new("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true),
                new("Packages/gg.easy.airship/Runtime/Scenes/Login.unity", true)
            };
            EditorBuildSettings.scenes = scenes;

            FileUtil.DeleteFileOrDirectory("build/StandaloneLinux64");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.dedicatedServerOptimizations = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            EditorUserBuildSettings.managedDebuggerFixedPort = 55000;
            var options = new BuildPlayerOptions();
            options.scenes = new[] { "Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity" };
            options.locationPathName = $"build/StandaloneLinux64/{ServerExecutableName}";
            options.target = BuildTarget.StandaloneLinux64;
            options.extraScriptingDefines = new[] { "UNITY_SERVER", "AIRSHIP_PLAYER", "AIRSHIP_INTERAL" };
            options.subtarget = (int)StandaloneBuildSubtarget.Server;
            options.options |= BuildOptions.Development; //Enable the profiler
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Linux succeeded with size: {FormatBytes(summary)}");
                    break;
                case BuildResult.Failed:
                    Debug.Log("Build Linux failed");
                    break;
                default:
                    Debug.Log("Build Linux unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac (Staging)", priority = 80)]
#endif
        public static void BuildMacClientStaging() {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, new string[] {"AIRSHIP_STAGING", "AIRSHIP_PLAYER", "AIRSHIP_INTERNAL"});
            BuildMacClient();
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac", priority = 80)]
#endif
        public static void BuildMacClient() {
#if UNITY_EDITOR_OSX
            OnBuild();
            CreateAssetBundles.ResetScenes();

            UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            PlayerSettings.SplashScreen.show = false;
            
            // Grab icons
            var sizes = new[] { 1024, 512, 256, 128, 64, 48, 32, 16 };
            var icons = new Texture2D[8];
            for (var i = 0; i < sizes.Length; i++) {
                var iconSize = sizes[i];
                icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/App Icons/logo_mac/mac_icon_{iconSize}.png");
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = $"build/client_mac/{ClientExecutableName}";
            options.target = BuildTarget.StandaloneOSX;
            // options.options = BuildOptions.Development;

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Mac succeeded with size: {FormatBytes(summary)}");
                    // EditorUtility.RevealInFinder(Application.dataPath + "/" + options.locationPathName);
                    EditorUtility.RevealInFinder(report.summary.outputPath);
                    break;
                case BuildResult.Failed:
                    Debug.LogError("Build Mac failed");
                    break;
                default:
                    Debug.LogError("Build Mac unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac (Development)", priority = 80)]
#endif
        public static void BuildMacDevelopmentClient() {
#if UNITY_EDITOR_OSX
            OnBuild();
            CreateAssetBundles.ResetScenes();

            UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = $"build/client_mac/{ClientExecutableName}";
            options.target = BuildTarget.StandaloneOSX;
            options.options = BuildOptions.Development | BuildOptions.ConnectWithProfiler;

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Mac succeeded with size: {FormatBytes(summary)}");
                    // EditorUtility.RevealInFinder(Application.dataPath + "/" + options.locationPathName);
                    EditorUtility.RevealInFinder(report.summary.outputPath);
                    break;
                case BuildResult.Failed:
                    Debug.LogError("Build Mac failed");
                    break;
                default:
                    Debug.LogError("Build Mac unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

        public static void BuildIOSClient(bool development) {
#if UNITY_EDITOR_OSX
            OnBuild();
            CreateAssetBundles.ResetScenes();

            UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = "build/client_ios";
            options.target = BuildTarget.iOS;
            if (development == true) {
                options.options = BuildOptions.Development;
            }

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build iOS succeeded with size: {FormatBytes(summary)}");
                    // EditorUtility.RevealInFinder(Application.dataPath + "/" + options.locationPathName);
                    EditorUtility.RevealInFinder(report.summary.outputPath);
                    break;
                case BuildResult.Failed:
                    Debug.LogError("Build iOS failed");
                    break;
                default:
                    Debug.LogError("Build iOS unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/iOS", priority = 80)]
        public static void BuildIOSClientMenuItem() {
            BuildIOSClient(false);
        }

        [MenuItem("Airship/Create Binary/Client/iOS (Development)", priority = 80)]
        public static void BuildIOSDevelopmentClientMenuItem() {
            BuildIOSClient(true);
        }
#endif

        public static void BuildWindowsClientStaging() {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, new string[] {"AIRSHIP_STAGING", "AIRSHIP_PLAYER", "AIRSHIP_INTERNAL"});
            BuildWindowsClient();
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows", priority = 80)]
#endif
        public static void BuildWindowsClient() {
#if UNITY_EDITOR
            OnBuild();
            CreateAssetBundles.ResetScenes();

            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, Array.Empty<Texture2D>(), IconKind.Application);
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            var options = new BuildPlayerOptions();
            
            options.scenes = scenes;
            options.locationPathName = $"build/client_windows/{ClientExecutableName}.exe";
            options.target = BuildTarget.StandaloneWindows64;

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Windows succeeded with size: {FormatBytes(summary)}");
                    break;
                case BuildResult.Failed:
                    Debug.Log("Build Windows failed");
                    break;
                default:
                    Debug.Log("Build Windows unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows (Development)", priority = 80)]
#endif
        public static void BuildWindowsClientProfiler() {
#if UNITY_EDITOR
            OnBuild();
            CreateAssetBundles.ResetScenes();

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);

            var options = new BuildPlayerOptions();

            options.scenes = scenes;
            options.locationPathName = $"build/client_windows/{ClientExecutableName}.exe";
            options.target = BuildTarget.StandaloneWindows64;
            options.options |= BuildOptions.Development | BuildOptions.ConnectWithProfiler;

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Windows succeeded with size: {FormatBytes(summary)}");
                    break;
                case BuildResult.Failed:
                    Debug.Log("Build Windows failed");
                    break;
                default:
                    Debug.Log("Build Windows unexpected result:" + summary.result);
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }
    }
}
#endif
