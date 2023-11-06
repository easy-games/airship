using System;
using Airship.DevConsole;
using DavidFDev.DevConsole;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Serialization;

struct ServerConsoleBroadcast : IBroadcast
{
    public string message;
    public LogType logType;
}

[LuauAPI]
public class ServerConsole : MonoBehaviour
{
    [SerializeField] public bool RemoteLogging = false;
    
    private void OnEnable()
    {
        if (RunCore.IsClient() && InstanceFinder.ClientManager)
        {
            InstanceFinder.ClientManager.RegisterBroadcast<ServerConsoleBroadcast>(OnServerConsoleBroadcast);
        }

        if (RunCore.IsServer())
        {
            Application.logMessageReceived += LogCallback;
        }
    }

    void LogCallback(string message, string stackTrace, LogType type)
    {
        // string s = message;
        // if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        // {
        //     s += " " + stackTrace;
        // }
        // SendServerLogMessage(s);
    }

    private void OnDisable()
    {
        if (RunCore.IsClient() && InstanceFinder.ClientManager)
        {
            InstanceFinder.ClientManager.UnregisterBroadcast<ServerConsoleBroadcast>(OnServerConsoleBroadcast);
        }

        if (RunCore.IsServer())
        {
            Application.logMessageReceived -= LogCallback;
        }
    }

    private void SendServerLogMessage(string message, LogType logType = LogType.Log)
    {
        if (RunCore.IsServer() && RemoteLogging && InstanceFinder.ServerManager.Started)
        {
            InstanceFinder.ServerManager.Broadcast(new ServerConsoleBroadcast()
            {
                message = message,
                logType = logType
            });
        }
    }

    private void OnServerConsoleBroadcast(ServerConsoleBroadcast args) {
        if (args.logType == LogType.Log) {
            DevConsole.Log(args.message, LogContext.Server);
        } else if (args.logType == LogType.Error || args.logType == LogType.Exception || args.logType == LogType.Assert) {
            DevConsole.LogError(args.message, LogContext.Server);
        } else if (args.logType == LogType.Warning) {
            DevConsole.LogWarning(args.message, LogContext.Server);
        } else {
            DevConsole.Log(args.message, LogContext.Server);
        }
    }
}