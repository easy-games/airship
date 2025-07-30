using System;
using Code.Bootstrap;
using UnityEngine;


/// <summary>
/// This singleton is exists in the CoreScene, MainMenu, and Login scene.
/// </summary>
public class AirshipEntryPoint : Singleton<AirshipEntryPoint> {
    private void Start() {
        Debug.unityLogger.logHandler = new AirshipLogHandler();
    }
}