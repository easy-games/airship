using System;
using Airship.DevConsole;
using Code.Analytics;
using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (RunCore.IsClient()) {
#if UNITY_STANDALONE_OSX || true
            QualitySettings.vSyncCount = 0;
#endif
            Application.targetFrameRate = (int)Math.Ceiling(Screen.currentResolution.refreshRateRatio.value);
            Application.logMessageReceived += AnalyticsRecorder.RecordLogMessageToAnalytics;
        }
    }
}