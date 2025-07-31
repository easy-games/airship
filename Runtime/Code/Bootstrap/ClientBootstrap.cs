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
            Application.targetFrameRate = -1;
#else
            Application.targetFrameRate = (int)Math.Ceiling(Screen.currentResolution.refreshRateRatio.value);\
#endif
            Application.logMessageReceived += AnalyticsRecorder.RecordLogMessageToAnalytics;
        }
    }
}