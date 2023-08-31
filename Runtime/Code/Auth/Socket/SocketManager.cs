using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Auth;
using Code.Util;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.JsonSerializer;
using SocketIOClient.Transport;
using UnityEngine;

[LuauAPI]
public class SocketManager : Singleton<SocketManager> {
    private List<SocketPacket> queuedPackets = new();
    public SocketIO socket;
    private bool isScriptListening = false;
    public event Action<string, string> OnEvent;

    public static async Task<bool> ConnectAsync(string url, string authToken) {
        if (Instance.socket == null) {
            print("Creating new socket connection.");
            // Needed to force creation of the GameObject.
            var test = UnityMainThreadDispatcher.Instance;
            Instance.socket = new SocketIOClient.SocketIO(url, new SocketIOOptions() {
                Auth = new Dictionary<string, string> {
                    { "token", authToken }
                },
                Transport = TransportProtocol.WebSocket,
                Reconnection = true
            });
            Instance.socket.JsonSerializer = new NewtonsoftJsonSerializer();
            LuauCore.onResetInstance += LuauCore_OnResetInstance;

            Instance.socket.OnAny((eventName, response) => {
                string data = response.GetValue().ToString();
                if (Instance.isScriptListening) {
                    UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnEvent(eventName, data));
                } else {
                    Instance.queuedPackets.Add(new SocketPacket() {
                        eventName = eventName,
                        data = data
                    });
                }
            });
        }

        if (!Instance.socket.Connected) {
            try {
                await Instance.socket.ConnectAsync();
            } catch (Exception e) {
                Debug.LogError(e);
                return false;
            }
        }

        return Instance.socket.Connected;
    }

    private void OnDisable() {
        if (Instance.socket != null) {
            Instance.socket.Dispose();
            Instance.socket.DisconnectAsync();
            Instance.socket = null;
        }
        LuauCore.onResetInstance -= LuauCore_OnResetInstance;
    }

    private IEnumerator FireOnEvent(string eventName, string data) {
        Instance.OnEvent?.Invoke(eventName, data);
        yield return null;
    }

    private static void LuauCore_OnResetInstance() {
        Instance.isScriptListening = false;
    }

    public static async Task EmitAsync(string eventName, string data) {
        if (Instance.socket == null) return;

        var json = Instance.socket.JsonSerializer.Deserialize<JObject>(data);
        await Instance.socket.EmitAsync(eventName, json);
    }

    public static bool IsConnected() {
        return Instance.socket != null && Instance.socket.Connected;
    }

    public static void SetScriptListening(bool val) {
        Instance.isScriptListening = val;
        if (val) {
            foreach (var packet in Instance.queuedPackets) {
                Instance.OnEvent?.Invoke(packet.eventName, packet.data);
            }
            Instance.queuedPackets.Clear();
        }
    }
}