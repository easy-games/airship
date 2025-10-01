using System;
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
        });
#endif
    }
}