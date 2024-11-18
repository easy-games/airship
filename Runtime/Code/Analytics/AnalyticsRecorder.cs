using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Analytics {
    public static class AnalyticsRecorder {
        private static List<ReportableError> errors = new();
        public static StartupConfig? startupConfig { get; private set; }
        public static void InitGame(StartupConfig config) {
            AnalyticsRecorder.startupConfig = config;
        }

        public static void RecordLogMessageToAnalytics(string message, string stackTrace, LogType logType) {
            if (startupConfig == null) {
                // Currently we are only reporting errors at a game level
                return;
            }
            var now = DateTime.UtcNow;
            if (logType == LogType.Error) {
                Debug.Log("Recording error: " + message + " - " + stackTrace);
                errors.Add(new ReportableError {
                    timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                    stackTrace = stackTrace,
                    message = message,
                });

                if (errors.Count > 100) {
                    errors.RemoveAt(0);
                }
            }
        }

    public static List<ReportableError> GetAndClearErrors() {
            var errorsToReturn = errors;
            errors = new();
            return errorsToReturn;
        }

    }
}