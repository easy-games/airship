using System.Collections.Generic;
using Airship.DevConsole;
using FishNet;
using FishNet.Broadcast;
using FishNet.Transporting;
using UnityEngine;

namespace Code.RemoteConsole {
    struct ServerConsoleBroadcast : IBroadcast {
        public string message;
        public LogType logType;
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
                    print("Sending startup messages to " + conn.ClientId);
                    foreach (var startupMessage in startupMessages) {
                        InstanceFinder.ServerManager.Broadcast<ServerConsoleBroadcast>(conn, startupMessage);
                    }
                });
            }
        }

        void LogCallback(string message, string stackTrace, LogType type)
        {
            string s = message;
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                s += " " + stackTrace;
            }
            SendServerLogMessage(s);
        }

        private void OnDisable() {
            if (RunCore.IsClient() && InstanceFinder.ClientManager) {
                InstanceFinder.ClientManager.UnregisterBroadcast<ServerConsoleBroadcast>(OnServerConsoleBroadcast);
            }

            if (RunCore.IsServer()) {
                Application.logMessageReceived -= LogCallback;
            }
        }

        private void SendServerLogMessage(string message, LogType logType = LogType.Log) {
            if (RunCore.IsServer() && RemoteLogging && InstanceFinder.ServerManager.Started) {

                if (this.startupMessages.Count < maxStartupMessages) {
                    this.startupMessages.Add(new ServerConsoleBroadcast() {
                        message = message,
                        logType = logType,
                        startup = true,
                    });
                }

                var packet = new ServerConsoleBroadcast() {
                    message = message,
                    logType = logType,
                    startup = false,
                };
                InstanceFinder.ServerManager.Broadcast(packet, false);
            }
        }

        private void OnServerConsoleBroadcast(ServerConsoleBroadcast args, Channel channel) {
            if (args.logType == LogType.Log) {
                // Debug.Log("[Server]: " + args.message);
                DevConsole.Log(args.message, LogContext.Server);
            } else if (args.logType == LogType.Error || args.logType == LogType.Exception || args.logType == LogType.Assert) {
                // Debug.LogError("[Server]: " + args.message);
                DevConsole.LogError(args.message, LogContext.Server);
            } else if (args.logType == LogType.Warning) {
                // Debug.LogWarning("[Server]: " + args.message);
                DevConsole.LogWarning(args.message, LogContext.Server);
            } else {
                // Debug.Log("[Server]: " + args.message);
                DevConsole.Log(args.message, LogContext.Server);
            }
        }
    }
}