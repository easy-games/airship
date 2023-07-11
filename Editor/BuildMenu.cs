#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
#if UNITY_STANDALONE_OSX
using UnityEditor.OSXStandalone;
#endif
using UnityEngine;

namespace Editor
{
    public class BuildMenu
    {
        [MenuItem("EasyGG/Build/Server/Linux")]
        public static void BuildLinuxServer()
        {
            // CreateAssetBundles.BuildLinuxPlayerAssetBundlesAsLocal();
            EditorBuildSettingsScene[] scenes = new[]
            {
                new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
            
            FileUtil.DeleteFileOrDirectory("build/StandaloneLinux64");

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.managedDebuggerFixedPort = 55000;
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] {"Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity"};
            options.locationPathName = "build/StandaloneLinux64/StandaloneLinux64";
            options.target = BuildTarget.StandaloneLinux64;
            options.extraScriptingDefines = new[] { "UNITY_SERVER" };
            options.subtarget = (int)(StandaloneBuildSubtarget.Server);
            options.options |= BuildOptions.Development;    //Enable the profiler
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log("Build Linux succeeded with size: " + Math.Floor(summary.totalSize / 1000000f) + " mb");
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
        
        [MenuItem("EasyGG/Build/Server/Mac")]
        public static void BuildMacServer()
        {
#if UNITY_EDITOR_OSX
            CreateAssetBundles.BuildMacPlayerAssetBundlesAsLocal();
            
            FileUtil.DeleteFileOrDirectory("build/server_mac");

            UserBuildSettings.architecture = MacOSArchitecture.x64;
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] {"Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity"};
            options.locationPathName = "build/server_mac/server_mac";
            
            options.target = BuildTarget.StandaloneOSX;
            
            // options.
            options.extraScriptingDefines = new[] { "UNITY_SERVER" };
            // options.options = BuildOptions.EnableHeadlessMode;
            options.subtarget = (int)(StandaloneBuildSubtarget.Server);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log("Build Mac succeeded with size: " + Math.Floor(summary.totalSize / 1000000f) + " mb");
                    break;
                case BuildResult.Failed:
                    Debug.Log("Build Mac failed");
                    break;
                default:
                    Debug.Log("Build Mac unexpected result:" + summary.result);
                    break;
            }
#endif
        }
        
        [MenuItem("EasyGG/Build/Client/Mac")]
        public static void BuildMacClient()
        {
#if UNITY_EDITOR_OSX
            CreateAssetBundles.ResetScenes();
            // CreateAssetBundles.BuildMacPlayerAssetBundlesAsLocal();
            
            // FileUtil.DeleteFileOrDirectory("build/client_mac");

            UserBuildSettings.architecture = MacOSArchitecture.x64;
            PlayerSettings.SplashScreen.show = false;
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] {"Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", "Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity"};
            options.locationPathName = "build/client_mac/client_mac";
            options.target = BuildTarget.StandaloneOSX;
            // options.
            // options.extraScriptingDefines = new[] { "UNITY_SERVER" };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log("Build Mac succeeded with size: " + Math.Floor(summary.totalSize / 1000000f) + " mb");
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
        
        [MenuItem("EasyGG/Build/Client/Windows")]
        public static void BuildWindowsClient()
        {
#if UNITY_EDITOR
            CreateAssetBundles.ResetScenes();
            // CreateAssetBundles.BuildMacPlayerAssetBundlesAsLocal();
            
            // FileUtil.DeleteFileOrDirectory("build/client_mac");
            
            BuildPlayerOptions options = new BuildPlayerOptions();
            PlayerSettings.SplashScreen.show = false;
            options.scenes = new[] {"Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", "Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity"};
            options.locationPathName = "build/client_windows/client_windows.exe";
            options.target = BuildTarget.StandaloneWindows64;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log("Build Windows succeeded with size: " + Math.Floor(summary.totalSize / 1000000f) + " mb");
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