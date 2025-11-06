#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Code.Bootstrap;
using NUnit;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
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

        public static void OnBuild() {
            PhysicsSetup.Setup();
        }

        public static void BuildLinuxServerStaging() {
            BuildLinuxServer(new []{"AIRSHIP_STAGING"});
        }
        
#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Server/Linux", priority = 80)]
#endif
        public static void BuildLinuxServerProduction() {
            BuildLinuxServer(new string[]{});
        }


        public static void BuildLinuxServer(string[] extraDefines) {
            OnBuild();
            FileUtil.DeleteFileOrDirectory("build/StandaloneLinux64");

            BuildProfile buildProfile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Dedicated Server (Linux).asset");
            buildProfile.overrideGlobalScenes = true;
            buildProfile.scenes = new[] {
                new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true)
            };
            buildProfile.scriptingDefines = new[] { "UNITY_SERVER", "AIRSHIP_PLAYER", "AIRSHIP_INTERAL" }.Concat(extraDefines).ToArray();
            BuildProfile.SetActiveBuildProfile(buildProfile);
            
            Debug.Log("Building with " + buildProfile.scenes.Length + " scenes");

            var options = new BuildPlayerWithProfileOptions() {
                buildProfile = buildProfile,
                locationPathName = $"build/StandaloneLinux64/{ServerExecutableName}",
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Linux succeeded with size: {FormatBytes(summary)}");
                    break;
                case BuildResult.Failed:
                    Debug.Log("Build Linux failed");
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.Log("Build Linux unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
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

            CreateAssetBundles.SwapToQualityLevel("Normal");

            UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            PlayerSettings.SplashScreen.show = false;
            
            // Grab icons
            // var sizes = new[] { 1024, 512, 256, 128, 64, 48, 32, 16 };
            // var icons = new Texture2D[8];
            // for (var i = 0; i < sizes.Length; i++) {
            //     var iconSize = sizes[i];
            //     icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/App Icons/logo_mac/mac_icon_{iconSize}.png");
            // }
            // PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
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
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.LogError("Build Mac unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac (Development)", priority = 80)]
#endif
        public static void BuildMacClientDev() {
#if UNITY_EDITOR_OSX
            OnBuild();
            CreateAssetBundles.ResetScenes();

            CreateAssetBundles.SwapToQualityLevel("Normal");

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
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.LogError("Build Mac unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

        public static void BuildIOSClient(bool development, bool staging) {
#if AIRSHIP_PLAYER
            OnBuild();
            CreateAssetBundles.ResetScenes();

            CreateAssetBundles.SwapToQualityLevel("Low");

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = "build/client_ios";
            options.target = BuildTarget.iOS;

            var extraDefines = new List<string>();
            if (staging) {
                extraDefines.Add("AIRSHIP_STAGING");
                extraDefines.Add("AIRSHIP_INTERNAL");
            }
            options.extraScriptingDefines = extraDefines.ToArray();

            if (development == true) {
                options.options = BuildOptions.Development | BuildOptions.ConnectWithProfiler;
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
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.LogError("Build iOS unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

        public enum AndroidBuildType {
            DevelopmentAPK,
            ReleaseAPK,
            ReleaseAAB,
        }

        public enum AndroidEnvironment {
            Production,
            Staging,
        }
        
        public static void BuildAndroidClient(AndroidBuildType buildType, AndroidEnvironment environment) {
#if AIRSHIP_PLAYER
           StreamingAssets.SetCoreMaterialPlatform(AirshipPlatform.Android);
            
            var development = buildType == AndroidBuildType.DevelopmentAPK;
            var buildApk = buildType != AndroidBuildType.ReleaseAAB;

            CreateAssetBundles.SwapToQualityLevel("Low");
            
            OnBuild();
            CreateAssetBundles.ResetScenes();

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.splitApplicationBinary = !buildApk;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            
            PlayerSettings.Android.keyaliasName = environment switch {
                AndroidEnvironment.Production => "airship",
                AndroidEnvironment.Staging => "airship-staging",
                _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null)
            };

            Dictionary<string, string> envOptions = GetValidatedOptions();
            if (envOptions.TryGetValue("androidKeystoreName", out string keystoreName) &&
                !string.IsNullOrEmpty(keystoreName))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystoreName;
            }
            if (envOptions.TryGetValue("androidKeystorePass", out string keystorePass) &&
                !string.IsNullOrEmpty(keystorePass))
                PlayerSettings.Android.keystorePass = keystorePass;
            if (envOptions.TryGetValue("androidKeyaliasName", out string keyaliasName) &&
                !string.IsNullOrEmpty(keyaliasName))
                PlayerSettings.Android.keyaliasName = keyaliasName;
            if (envOptions.TryGetValue("androidKeyaliasPass", out string keyaliasPass) &&
                !string.IsNullOrEmpty(keyaliasPass))
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
            
            var editorBuildScenes = new List<EditorBuildSettingsScene>();
            foreach (var sceneName in scenes) {
                editorBuildScenes.Add(new EditorBuildSettingsScene(sceneName, true));
            }
            
            BuildProfile buildProfile;
            if (development) {
                buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Android Debug.asset");
            } else {
                buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Android Google Play.asset");
            }

            var defines = new List<string>();
            defines.Add("AIRSHIP_PLAYER");
            if (environment == AndroidEnvironment.Staging) {
                defines.Add("AIRSHIP_STAGING");
                defines.Add("AIRSHIP_INTERNAL");
            }
            buildProfile.scriptingDefines = defines.ToArray();
            
            var options = new BuildPlayerWithProfileOptions();
            buildProfile.overrideGlobalScenes = true;
            buildProfile.scenes = editorBuildScenes.ToArray();
            options.buildProfile = buildProfile;
            options.locationPathName = $"build/client_android/{ClientExecutableName}.{(buildApk ? "apk" : "aab")}";
            if (development) {
                options.options = BuildOptions.Development | BuildOptions.ConnectWithProfiler;
            }
        
            var  report = BuildPipeline.BuildPlayer(options);
            
            var summary = report.summary;
            switch (summary.result) {
                case BuildResult.Succeeded:
                    Debug.Log($"Build Android succeeded with size: {FormatBytes(summary)}");
                    EditorUtility.RevealInFinder(report.summary.outputPath);
                    break;
                case BuildResult.Failed:
                    Debug.LogError("Build Android failed");
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.LogError("Build Android unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }
            
            StreamingAssets.ResetCoreMaterials();
            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/iOS", priority = 80)]
        public static void BuildIOSClientMenuItem() {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out var defines);
            if (defines.Contains("AIRSHIP_STAGING")) {
                var list = new List<string>(defines);
                list.Remove("AIRSHIP_STAGING");
                defines = list.ToArray();
            }
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines);

            BuildIOSClient(false, false);
        }

        [MenuItem("Airship/Create Binary/Client/iOS (Development)", priority = 80)]
        public static void BuildIOSDevelopmentClientMenuItem() {
            BuildIOSClient(true, false);
        }

        [MenuItem("Airship/Create Binary/Client/iOS (Staging)", priority = 80)]
        public static void BuildIOSClientStagingMenuItem() {
            Debug.Log("Building iOS staging client..");
            BuildIOSClient(false, true);
        }

        [MenuItem("Airship/Create Binary/Client/iOS (Staging Development)", priority = 80)]
        public static void BuildIOSClientStagingDevelopmentMenuItem() {
            Debug.Log("Building iOS staging development client..");
            BuildIOSClient(true, true);
        }

        [MenuItem("Airship/Create Binary/Client/Android/Android Release (Google Play)", priority = 10)]
        public static void BuildAndroidClientMenuItem() {
            BuildAndroidClient(AndroidBuildType.ReleaseAAB, AndroidEnvironment.Production);
        }
        
        [MenuItem("Airship/Create Binary/Client/Android/Android APK", priority = 80)]
        public static void BuildAndroidProdAPK() {
            BuildAndroidClient(AndroidBuildType.ReleaseAPK, AndroidEnvironment.Production);
        }

        [MenuItem("Airship/Create Binary/Client/Android/Android APK (Development)", priority = 80)]
        public static void BuildAndroidDevelopmentClientMenuItem() {
            BuildAndroidClient(AndroidBuildType.DevelopmentAPK, AndroidEnvironment.Production);
        }
        
        [MenuItem("Airship/Create Binary/Client/Android/Android Staging APK", priority = 150)]
        public static void BuildAndroidProdStagingAPK() {
            BuildAndroidClient(AndroidBuildType.ReleaseAPK, AndroidEnvironment.Staging);
        }

        [MenuItem("Airship/Create Binary/Client/Android/Android Staging APK (Development)", priority = 150)]
        public static void BuildAndroidDevelopmentStagingClientMenuItem() {
            BuildAndroidClient(AndroidBuildType.DevelopmentAPK, AndroidEnvironment.Staging);
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

            CreateAssetBundles.SwapToQualityLevel("Normal");

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
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.Log("Build Windows unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows (Development)", priority = 80)]
#endif
        public static void BuildWindowsClientDev() {
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
#if GAME_CI
                    EditorApplication.Exit(1);
#endif
                    break;
                default:
                    Debug.Log("Build Windows unexpected result:" + summary.result);
#if GAME_CI
                    EditorApplication.Exit(2);
#endif
                    break;
            }

            CreateAssetBundles.AddAllGameBundleScenes();
#endif
        }

        // From Game CI example
        private static Dictionary<string, string> GetValidatedOptions() {
            ParseCommandLineArguments(out Dictionary<string, string> validatedOptions);

            if (!validatedOptions.TryGetValue("projectPath", out string _))
            {
                Console.WriteLine("Missing argument -projectPath");
                EditorApplication.Exit(110);
            }

            if (validatedOptions.TryGetValue("buildTarget", out var buildTarget))
            {
                if (!Enum.IsDefined(typeof(BuildTarget), buildTarget ?? string.Empty))
                {
                    Console.WriteLine($"{buildTarget} is not a defined {nameof(BuildTarget)}");
                    EditorApplication.Exit(121);
                }
            }
            else if (!validatedOptions.TryGetValue("activeBuildProfile", out string _))
            {
                Console.WriteLine("Missing argument -buildTarget or -activeBuildProfile");
                EditorApplication.Exit(120);
            }

            if (!validatedOptions.TryGetValue("customBuildPath", out string _))
            {
                Console.WriteLine("Missing argument -customBuildPath");
                EditorApplication.Exit(130);
            }

            return validatedOptions;
        }
        
        private static readonly string[] Secrets =
            {"androidKeystorePass", "androidKeyaliasName", "androidKeyaliasPass"};

        private static void ParseCommandLineArguments(out Dictionary<string, string> providedArguments)  {
            providedArguments = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();

            // Extract flags with optional values
            for (int current = 0, next = 1; current < args.Length; current++, next++)
            {
                // Parse flag
                bool isFlag = args[current].StartsWith("-");
                if (!isFlag) continue;
                string flag = args[current].TrimStart('-');

                // Parse optional value
                bool flagHasValue = next < args.Length && !args[next].StartsWith("-");
                string value = flagHasValue ? args[next].TrimStart('-') : "";
                bool secret = Secrets.Contains(flag);
                string displayValue = secret ? "*HIDDEN*" : "\"" + value + "\"";

                // Assign
                Console.WriteLine($"Found flag \"{flag}\" with value {displayValue}.");
                providedArguments.Add(flag, value);
            }
        }
        
    }
}
#endif
