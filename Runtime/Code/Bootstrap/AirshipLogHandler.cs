using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.Bootstrap {
    public class AirshipLogHandler : ILogHandler {
        private readonly ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

        public void LogFormat(LogType logType, Object context, string format, params object[] args) {
            string message = string.Format(format, args);

            // TextMeshPro warning about unsupported character.
            if (message.Contains("Unicode value \\u0007")) return;

            defaultLogHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context) {
            defaultLogHandler.LogException(exception, context);
        }
    }
}