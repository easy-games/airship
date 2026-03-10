#if AIRSHIP_PLAYER
using Unity.Services.Analytics;
using Unity.Services.Core;
#endif
using Code.Bootstrap;
using Sentry;
using UnityEngine;

#if UNITY_IOS && AIRSHIP_PLAYER
using SDK;
#endif


/// <summary>
/// This singleton exists in the CoreScene, MainMenu, and Login scene.
/// </summary>
public class AirshipEntryPoint : Singleton<AirshipEntryPoint> {
    private void Start() {
#if AIRSHIP_PLAYER
        Debug.unityLogger.logHandler = new AirshipLogHandler();

        SentrySdk.ConfigureScope(scope => {
            scope.SetExtra("platform", AirshipPlatformUtil.GetLocalPlatform().ToString());
            scope.SetExtra("deviceType", DeviceBridge.GetDeviceType().ToString());
            scope.SetExtra("graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString());

            if (RunCore.IsServer()) {
                scope.SetExtra("server", true);
            }
        });

        UnityServices.InitializeAsync();
        AnalyticsService.Instance.StartDataCollection();

        var playerSingletons = Resources.Load("InternalSingletons");
        if (playerSingletons) {
            var go = Instantiate(playerSingletons);
            go.name = "InternalSingletons";
        } else {
            Debug.LogError("Missing private InternalSingletons prefab.");
        }

#if UNITY_IOS && AIRSHIP_PLAYER
        TikTokConfig config = new TikTokConfig(
            "iOS_accessToken",
            "6480534389",
            "7613432807876345863",
            "Android_accessToken",
            "Android_appId",
            "Android_tiktokAppId"
        );
        TikTokBusinessSDK.InitializeSdk(config);
        TikTokBusinessSDK.StartTrack();
#endif
#endif
    }
}