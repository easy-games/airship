using System;
using System.Collections.Generic;
using Airship.DevConsole;
using FishNet;
using FishNet.Broadcast;
using FishNet.Transporting;
using UnityEngine;

namespace Code.RemoteConsole {
    struct ServerConsoleBroadcast : IBroadcast {
        public string message;
        public string stackTrace;
        public LogType logType;
        public string time;
        public bool startup;
    }

    struct RequestServerConsoleStartupLogs : IBroadcast {
        
    }

    [LuauAPI]
    public class ServerConsole : MonoBehaviour {
        [SerializeField] public bool RemoteLogging = false;
        private List<ServerConsoleBroadcast> startupMessages = new(100);
        private const int maxStartupMessages = 100;
    
        private void OnEnable() {
            if (RunCore.IsClient() && !RunCore.IsServer() && InstanceFinder.ClientManager) {
                InstanceFinder.ClientManager.RegisterBroadcast<ServerConsoleBroadcast>(OnServerConsoleBroadcast);

                InstanceFinder.ClientManager.OnAuthenticated += () => {
                    InstanceFinder.ClientManager.Broadcast<RequestServerConsoleStartupLogs>(new RequestServerConsoleStartupLogs());
                };
            }

            if (RunCore.IsServer() && !RunCore.IsClient()) {
                Application.logMessageReceived += LogCallback;

                InstanceFinder.ServerManager.RegisterBroadcast<RequestServerConsoleStartupLogs>((conn, data, channel) => {
                    // print("Sending startup messages to " + conn.ClientId);
                    foreach (var startupMessage in startupMessages) {
                        InstanceFinder.ServerManager.Broadcast<ServerConsoleBroadcast>(conn, startupMessage);
                    }
                });
            }
        }

        void LogCallback(string message, string stackTrace, LogType logType) {
            SendServerLogMessage(message, logType, stackTrace);
        }

        private void OnDisable() {
            if (RunCore.IsClient() && InstanceFinder.ClientManager) {
                InstanceFinder.ClientManager.UnregisterBroadcast<ServerConsoleBroadcast>(OnServerConsoleBroadcast);
            }

            if (RunCore.IsServer()) {
                Application.logMessageReceived -= LogCallback;
            }
        }

        private void SendServerLogMessage(string message, LogType logType = LogType.Log, string stackTrace = "") {
            if (RunCore.IsServer() && RemoteLogging && InstanceFinder.ServerManager.Started) {

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

                var packet = new ServerConsoleBroadcast() {
                    message = message,
                    logType = logType,
                    startup = false,
                    stackTrace = stackTrace,
                };
                InstanceFinder.ServerManager.Broadcast(packet, false);
            }
        }

        private void OnServerConsoleBroadcast(ServerConsoleBroadcast args, Channel channel) {
            DevConsole.console.OnLogMessageReceived(args.message, args.stackTrace, args.logType, LogContext.Server, args.time);
        }
    }
}