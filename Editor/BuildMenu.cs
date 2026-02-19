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

namespace Easy.Airship.Editor {
    public class BuildMenu {
        private const string ClientExecutableName = "airship";
        private const string ServerExecutableName = "StandaloneLinux64";
        
        public static string[] scenes = {
            "Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity",
            "Assets/Scenes/Login.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/Disconnected.unity",
            "Packages/gg.easy.airship/Runtime/Scenes/AirshipUpdateApp.unity",
        };
        
        /// <summary>
        /// List of all shared defines for Airship. Every built client & server will use all of these.
        /// </summary>
        private static string[] AirshipPlayerDefines = new string[] {
            // Airship
            "AIRSHIP_PLAYER",
            
            // Mirror
            "MIRROR", "MIRROR_79_OR_NEWER", "MIRROR_81_OR_NEWER", "MIRROR_82_OR_NEWER",
            "MIRROR_83_OR_NEWER", "MIRROR_84_OR_NEWER", "MIRROR_85_OR_NEWER", "MIRROR_86_OR_NEWER",
            "MIRROR_89_OR_NEWER",
            
            // True Shadow
            "LETAI_TRUESHADOW",
            
            // UniVoice
            "UNIVOICE_NETWORK_MIRROR", "UNIVOICE_FILTER_RNNOISE4UNITY",
        };
        /// <summary>
        /// Defines for builds with Steam support
        /// </summary>
        private static string[] SteamDefines = {
            "STEAMWORKS_NET",
        };
        /// <summary>
        /// List of additional scripting defines for all staging builds.
        /// </summary>
        private static string[] StagingAdditionalDefines = {
            "AIRSHIP_STAGING",
            "AIRSHIP_INTERNAL",
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

        public static void SetupProjectForBuild() {
            PhysicsSetup.Setup();
        }

        public static void BuildLinuxServerStaging() {
            BuildLinuxServer(StagingAdditionalDefines);
        }
        
#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Server/Linux", priority = 80)]
#endif
        public static void BuildLinuxServerProduction() {
            BuildLinuxServer();
        }


        public static void BuildLinuxServer(params string[] extraDefines) {
            SetupProjectForBuild();
            FileUtil.DeleteFileOrDirectory("build/StandaloneLinux64");

            BuildProfile buildProfile =
                AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Dedicated Server (Linux).asset");
            buildProfile.overrideGlobalScenes = true;
            buildProfile.scenes = new[] {
                new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true)
            };
            
            // Setup scripting defines
            var defines = new HashSet<string>(AirshipPlayerDefines);
            defines.Add("UNITY_SERVER");
            foreach (var extraDefine in extraDefines) defines.Add(extraDefine);
            buildProfile.scriptingDefines = defines.ToArray();
            
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
            InternalBuildMacClient(BuildOptions.None, StagingAdditionalDefines);
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac", priority = 80)]
#endif
        public static void BuildMacClient() {
            InternalBuildMacClient(BuildOptions.None);
        }
        
#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Mac (Development)", priority = 80)]
#endif
        public static void BuildMacClientDev() {
            InternalBuildMacClient(BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        private static void InternalBuildMacClient(BuildOptions buildOptions, params string[] additionalScriptingDefines) {
#if UNITY_EDITOR_OSX
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, AirshipPlayerDefines);
            
            SetupProjectForBuild();
            CreateAssetBundles.ResetScenes();

            CreateAssetBundles.SwapToQualityLevel("Normal");

            UserBuildSettings.architecture = OSArchitecture.x64ARM64;
            PlayerSettings.SplashScreen.show = false;
            
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = $"build/client_mac/{ClientExecutableName}";
            options.target = BuildTarget.StandaloneOSX;
            options.extraScriptingDefines = SteamDefines.Concat(additionalScriptingDefines).ToArray();
            options.options = buildOptions;

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
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, AirshipPlayerDefines);
            
            StreamingAssets.SetCoreMaterialPlatform(AirshipPlatform.iOS);
            SetupProjectForBuild();
            CreateAssetBundles.ResetScenes();

            CreateAssetBundles.SwapToQualityLevel("Low");

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = "build/client_ios";
            options.target = BuildTarget.iOS;

            if (staging) {
                options.extraScriptingDefines = StagingAdditionalDefines;
            }

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
            StreamingAssets.ResetCoreMaterials();
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
            
            SetupProjectForBuild();
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

            var defines = new List<string>(AirshipPlayerDefines);
            if (environment == AndroidEnvironment.Staging) {
                foreach (var stagingDefine in StagingAdditionalDefines) {
                    defines.Add(stagingDefine);
                }
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


#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows (Staging)", priority = 80)]
#endif
        public static void BuildWindowsClientStaging() {
            InternalBuildWindowsClient(BuildOptions.None, StagingAdditionalDefines);
        }

#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows", priority = 80)]
#endif
        public static void BuildWindowsClient() {
            InternalBuildWindowsClient(BuildOptions.None);
        }
        
#if AIRSHIP_PLAYER
        [MenuItem("Airship/Create Binary/Client/Windows (Development)", priority = 80)]
#endif
        public static void BuildWindowsClientDev() {
            InternalBuildWindowsClient(BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        public static void InternalBuildWindowsClient(BuildOptions buildOptions, params string[] additionalScriptingDefines) {
#if UNITY_EDITOR
            SetupProjectForBuild();
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, AirshipPlayerDefines);
            CreateAssetBundles.ResetScenes();

            CreateAssetBundles.SwapToQualityLevel("Normal");

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            
            var options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = $"build/client_windows/{ClientExecutableName}.exe";
            options.target = BuildTarget.StandaloneWindows64;
            options.options = buildOptions;
            options.extraScriptingDefines = SteamDefines.Concat(additionalScriptingDefines).ToArray();

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
#if GAME_CI
                EditorApplication.Exit(110);
#endif
            }

            if (validatedOptions.TryGetValue("buildTarget", out var buildTarget))
            {
                if (!Enum.IsDefined(typeof(BuildTarget), buildTarget ?? string.Empty))
                {
                    Console.WriteLine($"{buildTarget} is not a defined {nameof(BuildTarget)}");
#if GAME_CI
                    EditorApplication.Exit(121);
#endif
                }
            }
            else if (!validatedOptions.TryGetValue("activeBuildProfile", out string _))
            {
                Console.WriteLine("Missing argument -buildTarget or -activeBuildProfile");
#if GAME_CI
                EditorApplication.Exit(120);
#endif
            }

            if (!validatedOptions.TryGetValue("customBuildPath", out string _))
            {
                Console.WriteLine("Missing argument -customBuildPath");
#if GAME_CI
                EditorApplication.Exit(130);
#endif
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
