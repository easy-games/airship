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
            var size = summary.totalSize;
            if (size < 1024) {
                return $"{size} bytes";
            }
            if (size < 1024 * 1024) {
                return $"{size / (1024.0 * 1024.0):F2} MB";
            }
            return $"{size / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private static void OnBuild() {
            PhysicsSetup.Setup(null);
        }

        public static void BuildLinuxServerProd() {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, "AIRSHIP_PRODUCTION");
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
            options.extraScriptingDefines = new[] { "UNITY_SERVER" };
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
        [MenuItem("Airship/Create Binary/Client/Mac", priority = 80)]
#endif
        public static void BuildMacClient() {
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
            options.options = BuildOptions.Development;

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

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows", priority = 80)]
#endif
        public static void BuildWindowsClient() {
#if UNITY_EDITOR
            OnBuild();
            CreateAssetBundles.ResetScenes();

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
