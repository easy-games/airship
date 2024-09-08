using System;
using System.Collections.Generic;
using System.IO;
using Airship.DevConsole;
using Mirror;
using UnityEngine;

namespace Code.RemoteConsole {
    struct ServerConsoleBroadcast : NetworkMessage {
        public string message;
        public string stackTrace;
        public LogType logType;
        public string time;
        public bool startup;
    }

    struct RequestServerConsoleStartupLogs : NetworkMessage {
        
    }

    [LuauAPI]
    public class ServerConsole : MonoBehaviour {
        private List<ServerConsoleBroadcast> startupMessages = new(100);
        private const int maxStartupMessages = 100;

        /// <summary>
        /// Called on client when client receives a remote log from server.
        /// </summary>
        /// <param name="args"></param>
        private void OnServerConsoleBroadcast(ServerConsoleBroadcast args) {
            DevConsole.console.OnLogMessageReceived(args.message, args.stackTrace, args.logType, LogContext.Server, args.time);
        }

        public void OnStartServer() {
            if (!RunCore.IsClient()) {
                Application.logMessageReceived += LogCallback;
            }
            Debug.Log("Registering startup log");
            NetworkServer.RegisterHandler<RequestServerConsoleStartupLogs>((conn, data) => {
                Debug.Log($"Sending {this.startupMessages.Count} startup logs to " + conn);
                foreach (var startupMessage in startupMessages) {
                    conn.Send(startupMessage);
                }
            }, false);
        }

        public void OnClientConnectedToServer() {
            if (!RunCore.IsServer()) {
                NetworkClient.RegisterHandler<ServerConsoleBroadcast>(OnServerConsoleBroadcast, false);
                Debug.Log("Sending request startup logs.");
                NetworkClient.Send(new RequestServerConsoleStartupLogs());
            }
        }

        public void Cleanup() {
            if (RunCore.IsClient()) {
                NetworkClient.UnregisterHandler<ServerConsoleBroadcast>();
            }
        }

        void LogCallback(string message, string stackTrace, LogType logType) {
            SendServerLogMessage(message, logType, stackTrace);
        }

        private void OnDisable() {
            if (RunCore.IsServer()) {
                Application.logMessageReceived -= LogCallback;
            }
        }

        private void SendServerLogMessage(string message, LogType logType = LogType.Log, string stackTrace = "") {
            if (RunCore.IsServer()) {
                var time = DateTime.Now.ToString("HH:mm:ss");
                if (this.startupMessages.Count < maxStartupMessages) {
                    this.startupMessages.Add(new ServerConsoleBroadcast() {
                        message = message,
                        logType = logType,
                        startup = true,
                        time = time,
                        stackTrace = stackTrace,
                    });
                }

                if (NetworkServer.active) {
                    var packet = new ServerConsoleBroadcast() {
                        message = message,
                        logType = logType,
                        startup = false,
                        stackTrace = stackTrace,
                        time = time,
                    };
                    NetworkServer.SendToAll(packet);
                }
            }
        }
    }
}