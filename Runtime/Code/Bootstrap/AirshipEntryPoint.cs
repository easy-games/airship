using System;
using Code.Bootstrap;
using Code.Bundles;
using Sentry;
using Sentry.Unity;
using UnityEngine;


/// <summary>
/// This singleton exists in the CoreScene, MainMenu, and Login scene.
/// </summary>
public class AirshipEntryPoint : Singleton<AirshipEntryPoint> {
    private void Start() {
#if AIRSHIP_PLAYER
        Debug.unityLogger.logHandler = new AirshipLogHandler();

        SentryUnity.Init(options => {
#if UNITY_IOS || UNITY_ANDROID
            options.Release = Application.version + "@" + AirshipVersion.GetVersionHash();
#else
            options.Release = AirshipVersion.GetVersionHash();
#endif
#if AIRSHIP_STAGING
                scope.Environment = "staging";
#else
                scope.Environment = "production";
#endif
        });

        SentrySdk.ConfigureScope(scope => {
            scope.SetExtra("platform", AirshipPlatformUtil.GetLocalPlatform().ToString());
            scope.SetExtra("deviceType", DeviceBridge.GetDeviceType().ToString());
            scope.SetExtra("graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString());

            if (RunCore.IsServer()) {
                scope.SetExtra("server", true);
            }
        });
#endif
    }
}