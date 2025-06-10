using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Airship.DevConsole;
using Code.Player;
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
        private static readonly ConcurrentQueue<string> logQueue = new();

        private string logPath;
        private string prevLogPath;
        private StreamWriter writer;

        private bool shuttingDown = false;
        private Task writeTask;

        private void Awake() {
            if (RunCore.IsClient() && !RunCore.IsServer()) {
                writeTask = Task.Run(ProcessQueue);
            }
        }

        private async Task ProcessQueue() {
            while (!shuttingDown) {
                while (logQueue.TryDequeue(out string msg)) {
                    try {
                        await writer.WriteLineAsync(msg);
                    } catch (Exception e) {
                        Debug.LogError("ServerLogger write failed: " + e);
                    }
                }

                await Task.Delay(100); // tune as needed
            }
        }

        private void OnDestroy() {
            shuttingDown = true;
            // writeTask?.Wait();
        }

        /// <summary>
        /// Called on client when client receives a remote log from server.
        /// </summary>
        /// <param name="args"></param>
        private void OnServerConsoleBroadcast(ServerConsoleBroadcast args) {
            if (!DevConsole.console.loggingEnabled) return;
            DevConsole.console.OnLogMessageReceived(args.message, args.stackTrace, args.logType, LogContext.Server, args.time);

            string timeStamped = $"[{DateTime.Now:HH:mm:ss}] {args.message}";
            logQueue.Enqueue(timeStamped);
        }

        public void OnStartServer() {
            if (!RunCore.IsClient()) {
                Application.logMessageReceived += LogCallback;
            }
            NetworkServer.RegisterHandler<RequestServerConsoleStartupLogs>((conn, data) => {
                foreach (var startupMessage in startupMessages) {
                    conn.Send(startupMessage);
                }
            }, false);
        }

        public void OnClientConnectedToServer() {
            if (!RunCore.IsServer()) {
                NetworkClient.RegisterHandler<ServerConsoleBroadcast>(OnServerConsoleBroadcast, false);
                NetworkClient.Send(new RequestServerConsoleStartupLogs());
            }

            // Setup logs
            if (RunCore.IsClient()) {
                string logDir = Path.GetDirectoryName(Application.consoleLogPath);
                logPath = Path.Combine(logDir, "Server.log");
                prevLogPath = Path.Combine(logDir, "Server-prev.log");

                // Rotate logs
                try {
                    if (File.Exists(prevLogPath)) File.Delete(prevLogPath);
                    if (File.Exists(logPath)) File.Move(logPath, prevLogPath);
                } catch (Exception e) {
                    Debug.LogError("Failed rotating server logs: " + e);
                }

                if (writer != null) {
                    writer.Close();
                }
                writer = new StreamWriter(logPath, false); // overwrite existing
                writer.AutoFlush = true;
            }
        }

        public void OnStopClient() {
            NetworkClient.UnregisterHandler<ServerConsoleBroadcast>();
            if (writer != null) {
                writer.Close();
                writer = null;
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
            if (RunCore.IsEditor()) {
                return;
            }
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
                    foreach (var player in PlayerManagerBridge.Instance.players) {
                        if (!string.IsNullOrEmpty(player.orgRoleName)) {
                            player.connectionToClient.Send(packet);
                        }
                    }
                }
            }
        }
    }
}