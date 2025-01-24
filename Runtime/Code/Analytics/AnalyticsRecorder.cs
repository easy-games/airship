using System;
using System.Collections.Generic;
using System.Globalization;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Analytics {
    public static class AnalyticsRecorder {
        private const int MaxErrorsBuffered = 100;
        
        private static readonly List<ReportableError> errors = new(MaxErrorsBuffered);
        public static StartupConfig? startupConfig { get; private set; }
        public static void InitGame(StartupConfig config) {
            AnalyticsRecorder.startupConfig = config;
        }

        public static void Reset() {
            errors.Clear();
            AnalyticsRecorder.startupConfig = null;
        }

        public static void RecordLogMessageToAnalytics(string message, string stackTrace, LogType logType) {
            if (startupConfig == null) {
                // Currently we are only reporting errors at a game level
                return;
            }

            if (logType == LogType.Error) {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

                var reportableError = new ReportableError {
                    timestamp = timestamp,
                    stackTrace = stackTrace,
                    message = message,
                };

                if (errors.Count >= MaxErrorsBuffered) {
                    errors.RemoveAt(0);
                }
            
                errors.Add(reportableError);
            }
        }

        public static List<ReportableError> GetAndClearErrors() {
            if (errors.Count <= 0) {
                return null;
            }
            
            var errorsToReturn = new List<ReportableError>(errors);
            errors.Clear();
            
            return errorsToReturn;
        }
    }
}
