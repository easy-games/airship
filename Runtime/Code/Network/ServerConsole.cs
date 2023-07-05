using System;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

struct ServerConsoleBroadcast : IBroadcast
{
    public string Message;
}

[LuauAPI]
public class ServerConsole : MonoBehaviour
{
    [SerializeField] public bool RemoteLogging = false;
    
    private void OnEnable()
    {
        if (RunCore.IsServer() && RunCore.IsEditor())
        {
            return;
        }
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
        string s = message;
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            s += " " + stackTrace;
        }
        SendServerLogMessage(s);
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

    private void SendServerLogMessage(string message)
    {
        if (RunCore.IsServer() && RemoteLogging && InstanceFinder.ServerManager.Started)
        {
            InstanceFinder.ServerManager.Broadcast(new ServerConsoleBroadcast()
            {
                Message = message
            });
        }
    }

    private void OnServerConsoleBroadcast(ServerConsoleBroadcast args)
    {
        Debug.Log("[Server]: " + args.Message);
    }
}