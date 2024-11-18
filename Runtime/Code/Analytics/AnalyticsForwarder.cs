using System.Collections.Generic;
using System.Linq;
using Code.Platform.Shared;
using Code.Platform.Server;
using UnityEngine;

namespace Code.Analytics
{
    public class AnalyticsForwarder : MonoBehaviour
    {
        private float minInterval = 10f;
        private float maxInterval = 15f;
        private bool isScheduled = false;
        private bool isAlreadySending = false;

        void Start()
        {
            if (!RunCore.IsServer())
            {
                return;
            }
            Debug.Log("Starting Analytics Forwarder");
            // Start the first action with a randomized initial delay
            ScheduleNextAction();
        }

        void SendMessages()
        {
            try
            {
                isScheduled = false;
                if (AnalyticsRecorder.startupConfig == null)  return;

                // Perform your action here
                var errors = AnalyticsRecorder.GetAndClearErrors();
                if (errors.Count <= 0) return;
                Debug.Log($"Flushing {errors.Count} errors!");
                var activePackages = AnalyticsRecorder.startupConfig.Value.packages.Select(p => new ActivePackage
                {
                    name = p.id,
                    version = p.publishVersionNumber,
                }).ToList();
                var message = new AirshipAnalyticsServerDto
                {
                    activePackages = activePackages,
                    errors = errors,
                    gameVersionId = AnalyticsRecorder.startupConfig.Value.GameAssetVersion,
                };
                var json = JsonUtility.ToJson(message);
                Debug.Log(json);

                if (isAlreadySending)
                {
                    Debug.Log("Analytics forwarder is already sending. Skipping.");
                    return;
                }

                isAlreadySending = true;
                AnalyticsServiceServerBackend.SendServerAnalytics(message).ContinueWith((t) =>
                {
                    isAlreadySending = false;
                    if (!t.Result.success)
                    {
                        Debug.LogError("Failed to send analytics: " + t.Result.error);
                    }
                });
            }
            finally
            {
                ScheduleNextAction();
            }

        }

        void ScheduleNextAction()
        {
            if (isScheduled)
            {
                return;
            }

            isScheduled = true;
            float delay = Random.Range(minInterval, maxInterval);
            Invoke(nameof(SendMessages), delay);
        }
    }
}
