#if AIRSHIP_PLAYER
using Unity.Services.Analytics;
using Unity.Services.Core;
#endif
using Code.Bootstrap;
using Sentry;
using UnityEngine;


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

        var playerSingletons = Resources.Load("AirshipPlayerSingletons");
        if (playerSingletons) {
            var go = Instantiate(playerSingletons);
            go.name = "AirshipPlayerSingletons";
        } else {
            Debug.LogError("Missing private AirshipPlayerSingletons prefab.");
        }
#endif
    }
}