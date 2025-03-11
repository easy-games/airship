#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Code.Bootstrap {
    public enum AirshipPlatform {
        iOS,
        Android,
        Mac,
        Windows,
        Linux
    }

    [LuauAPI]
    public class AirshipPlatformUtil {
        public static AirshipPlatform[] livePlatforms = new[]
        {
            AirshipPlatform.iOS,
            AirshipPlatform.Mac,
            AirshipPlatform.Windows,
            AirshipPlatform.Linux
        };

        public static string GetStringName(AirshipPlatform platform) {
            switch (platform) {
                case AirshipPlatform.iOS:
                    return "iOS";
                case AirshipPlatform.Android:
                    return "Android";
                case AirshipPlatform.Windows:
                    return "Windows";
                case AirshipPlatform.Mac:
                    return "Mac";
                case AirshipPlatform.Linux:
                    return "Linux";
                default:
                    return "Windows";
            }
        }

        public static AirshipPlatform GetLocalPlatform() {
#if UNITY_IOS
            return AirshipPlatform.iOS;
#endif
            return FromRuntimePlatform(Application.platform);
        }

        public static bool IsDeviceSimulator() {
#if UNITY_EDITOR
            if (Application.isEditor) {
                return false;
            } else {
                return true;
            }
#endif
            return false;
        }

        #if UNITY_EDITOR
        public static AirshipPlatform FromBuildTarget(BuildTarget buildTarget) {
            switch (buildTarget) {
                case BuildTarget.iOS:
                    return AirshipPlatform.iOS;
                case BuildTarget.Android:
                    return AirshipPlatform.Android;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return AirshipPlatform.Windows;
                case BuildTarget.StandaloneOSX:
                    return AirshipPlatform.Mac;
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.EmbeddedLinux:
                case BuildTarget.LinuxHeadlessSimulation:
                    return AirshipPlatform.Linux;
                default:
                    return AirshipPlatform.Linux;
            }
        }
        #endif

        public static AirshipPlatform FromRuntimePlatform(RuntimePlatform runtimePlatform) {
            switch (runtimePlatform) {
                case RuntimePlatform.IPhonePlayer:
                    return AirshipPlatform.iOS;
                case RuntimePlatform.Android:
                    return AirshipPlatform.Android;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsServer:
                    return AirshipPlatform.Windows;
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXServer:
                    return AirshipPlatform.Mac;
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxServer:
                    return AirshipPlatform.Linux;
                default:
                    return AirshipPlatform.Linux;
            }
        }

        #if UNITY_EDITOR
        public static BuildTarget ToBuildTarget(AirshipPlatform platform) {
            switch (platform) {
                case AirshipPlatform.Mac:
                    return BuildTarget.StandaloneOSX;
                case AirshipPlatform.Windows:
                    return BuildTarget.StandaloneWindows64;
                case AirshipPlatform.Linux:
                    return BuildTarget.StandaloneLinux64;
                case AirshipPlatform.iOS:
                    return BuildTarget.iOS;
                case AirshipPlatform.Android:
                    return BuildTarget.Android;
                default:
                    return BuildTarget.StandaloneLinux64;
            }
        }
#endif
    }
}