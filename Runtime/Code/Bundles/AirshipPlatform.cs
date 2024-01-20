#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Code.Bootstrap {
    public enum AirshipPlatform {
        IPhone,
        Android,
        Mac,
        Windows,
        Linux
    }

    public class AirshipPlatformUtil {
        public static AirshipPlatform[] livePlatforms = new[]
        {
            AirshipPlatform.Mac,
            // AirshipPlatform.Windows,
            AirshipPlatform.Linux
        };

        public static AirshipPlatform GetLocalPlatform() {
            return FromRuntimePlatform(Application.platform);
        }

        #if UNITY_EDITOR
        public static AirshipPlatform FromBuildTarget(BuildTarget buildTarget) {
            switch (buildTarget) {
                case BuildTarget.iOS:
                    return AirshipPlatform.IPhone;
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
                    return AirshipPlatform.IPhone;
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
                case AirshipPlatform.IPhone:
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