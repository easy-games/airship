using System.Collections.Generic;
using System.Linq;
using Code.Platform.Shared;
using Code.Platform.Server;
using UnityEngine;

namespace Code.Analytics {
    public class AnalyticsForwarder : MonoBehaviour {
        private float minInterval = 10f;
        private float maxInterval = 15f;
        private bool isScheduled = false;

        void Start()
        {
            if (!RunCore.IsServer()) {
                return;
            }
            Debug.Log("Starting Analytics Forwarder");
            // Start the first action with a randomized initial delay
            ScheduleNextAction();
        }

        void SendMessages()
        {
            if (AnalyticsRecorder.startupConfig == null) {
                Debug.Log("No startup config");
                return;
            }
            isScheduled = false;
            // Perform your action here
            var errors = AnalyticsRecorder.GetAndClearErrors();
            if (errors.Count <= 0) {
                Debug.Log("No errors to flush");
                return;
            }
            Debug.Log($"Flushing {errors.Count} errors!");
            var message = new AirshipAnalyticsServerDto {
                activePackages = new List<ActivePackage>(),
                errors = errors,
                gameVersionId = AnalyticsRecorder.startupConfig.Value.GameAssetVersion,
            };
            var json = JsonUtility.ToJson(message);
            Debug.Log(json);
            AnalyticsServiceServerBackend.SendServerAnalytics(message).ContinueWith((t) => {
                Debug.Log("Sent analytics");
                if (t.Result.success) {
                    Debug.Log("Successfully sent analytics");
                } else {
                    Debug.LogError("Failed to send analytics: " + t.Result.error);
                }
            });

            // Schedule the next action with a new jittered delay
            ScheduleNextAction();
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
