using System.Collections.Generic;
using System.Linq;
using Code.Bundles;
using Code.Platform.Shared;
using Code.Platform.Server;
using Code.Platform.Client;
using UnityEngine;

namespace Code.Analytics {
    public class AnalyticsForwarder : MonoBehaviour {
        private float minInterval = 10f;
        private float maxInterval = 15f;
        private bool isScheduled = false;
        private bool isAlreadySending = false;
        private ServerBootstrap serverBootstrap;

        private void Start() {
            this.serverBootstrap = FindObjectOfType<ServerBootstrap>();
            ScheduleNextAction();
        }

        private void SendServerMessages(List<ReportableError> errors, List<ActivePackage> activePackages) {
            var message = new AirshipAnalyticsServerDto {
                activePackages = activePackages,
                errors = errors,
                gameVersionId = AnalyticsRecorder.startupConfig.Value.GameAssetVersion,
                playerVersionId = AirshipVersion.GetVersionHash(),
            };

#if !UNITY_EDITOR
            isAlreadySending = true;
            AnalyticsServiceServerBackend.SendServerAnalytics(message).ContinueWith((t) => {
                isAlreadySending = false;
                if (!t.Result.success) {
                    Debug.LogError("Failed to send analytics: " + t.Result.error);
                }
            });
#endif
        }

        private void SendClientMessages(List<ReportableError> errors, List<ActivePackage> activePackages) {
            var message = new AirshipAnalyticsClientDto {
                activePackages = activePackages,
                errors = errors,
                gameId = AnalyticsRecorder.startupConfig.Value.GameBundleId,
                gameVersionId = AnalyticsRecorder.startupConfig.Value.GamePublishVersion,
                serverId = this.serverBootstrap.serverContext.serverId,
                playerVersionId = AirshipConst.playerVersion.ToString(),
            };

#if !UNITY_EDITOR
            isAlreadySending = true;
            AnalyticsServiceClient.SendClientAnalytics(message).ContinueWith((t) => {
                isAlreadySending = false;
                if (!t.Result.success) {
                    Debug.LogError("Failed to send analytics: " + t.Result.error);
                }
            });
#endif
        }

        private void SendMessages() {
            try {
                isScheduled = false;
                if (AnalyticsRecorder.startupConfig == null) return;
                if (isAlreadySending) return;

                // Perform your action here
                var errors = AnalyticsRecorder.GetAndClearErrors();
                if (errors == null || errors.Count <= 0) return;
                var activePackages = AnalyticsRecorder.startupConfig.Value.packages
                    .Where(p => !p.game) // Only report real packages, the game is added as a "package" so we need to filter it out
                    .Select(p => new ActivePackage {
                        name = p.id,
                        version = p.publishVersionNumber,
                    }).ToList();
                if (RunCore.IsServer()) {
                    SendServerMessages(errors, activePackages);
                } else {
                    SendClientMessages(errors, activePackages);
                }

            } finally {
                ScheduleNextAction();
            }

        }

        private void ScheduleNextAction() {
            if (isScheduled) {
                return;
            }

            isScheduled = true;
            float delay = Random.Range(minInterval, maxInterval);
            Invoke(nameof(SendMessages), delay);
        }
    }
}
