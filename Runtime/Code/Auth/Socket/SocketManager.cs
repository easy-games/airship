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
using UnityEngine.EventSystems;

[LuauAPI]
public class SocketManager : Singleton<SocketManager> {
    private List<SocketPacket> queuedIncomingPackets = new();
    private List<SocketPacket> queuedOutgoingPackets = new();
    public SocketIO socket;
    private bool isScriptListening = false;
    public event Action<string, string> OnEvent;

    private void Awake() {
        DontDestroyOnLoad(this);
    }

    public static async Task<bool> ConnectAsync(string url, string authToken) {
        if (Instance.socket == null) {
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
                    Instance.queuedIncomingPackets.Add(new SocketPacket() {
                        eventName = eventName,
                        data = data
                    });
                }
            });

            Instance.socket.OnConnected += (sender, args) => {
                foreach (var packet in Instance.queuedOutgoingPackets) {
                    EmitAsync(packet.eventName, packet.data);
                }
            };
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

    private async void OnDisable() {
        if (this.socket != null) {
            try {
                await this.socket.EmitAsync("disconnect-intent");
            } catch (Exception e) {
                Debug.LogError(e);
            }
            await this.socket.DisconnectAsync();
            this.socket = null;
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
        if (Instance.socket == null || !Instance.socket.Connected) {
            // queue outgoing
            Instance.queuedOutgoingPackets.Add(new SocketPacket {
                eventName = eventName,
                data = data
            });
            return;
        }

        var json = Instance.socket.JsonSerializer.Deserialize<JObject>(data);
        await Instance.socket.EmitAsync(eventName, json);
    }

    public static bool IsConnected() {
        return Instance.socket != null && Instance.socket.Connected;
    }

    public static void SetScriptListening(bool val) {
        Instance.isScriptListening = val;
        if (val) {
            foreach (var packet in Instance.queuedIncomingPackets) {
                Instance.OnEvent?.Invoke(packet.eventName, packet.data);
            }
            Instance.queuedIncomingPackets.Clear();
        }
    }
}