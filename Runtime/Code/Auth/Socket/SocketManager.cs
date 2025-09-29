using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Airship.DevConsole;
using Code.Auth;
using Code.Http.Internal;
using Code.Network;
using Code.Platform.Shared;
using Code.Util;
using Luau;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.JsonSerializer;
using SocketIOClient.Transport;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[LuauAPI]
public class SocketManager : Singleton<SocketManager> {
    private List<SocketPacket> queuedIncomingPackets = new();
    private List<SocketPacket> queuedOutgoingPackets = new();
    public SocketIO socket;
    private bool isScriptListening = false;
    public event Action<string, string> OnEvent;
    public event Action<string> OnDisconnected;

    private bool firstConnect = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private void onLoad() {
        firstConnect = true;
    }

#if UNITY_EDITOR
    static SocketManager() {
        EditorApplication.playModeStateChanged += ModeChanged;
    }

    static void ModeChanged(PlayModeStateChange change) {
        if (change == PlayModeStateChange.ExitingPlayMode) {
            Disconnect();
        }
    }
#endif

    private void Awake() {
        DontDestroyOnLoad(this);
    }

    private void Start() {
        DevConsole.AddCommand(Command.Create<string>("join", "", "Join a game from given gameId",
            Parameter.Create("gameId", "Game ID of the target game."), async (gameId) => {
                Debug.Log("Requesting transfer to game " + gameId + " ...");
                var packet = $"{{\"gameId\": \"{gameId}\"}}";
                var res = await InternalHttpManager.PostAsync(AirshipPlatformUrl.gameCoordinator + "/transfers/transfer/self", packet);
                if (res.success) {
                    Debug.Log("<color=green>Joining...</color>");
                }
                else {
                    Debug.LogError("Failed to join game: " + res.error);
                }
            }));
        
        DevConsole.AddCommand(Command.Create<string>("ping", "", "Measure ping to an airship server cluster",
            Parameter.Create("Ping Server IP", "IP of the Airship cluster ping server."), (serverIp) => {
               UdpPingTool.GetPing(serverIp, 1000).ContinueWith((result) => {
                   Debug.Log($"Latency to {serverIp.Trim()} is {result.Result}ms");
               });
            }));
    }

    public static async Task<bool> ConnectAsyncInternal() {
        // print("Connecting to socket with auth token: " + authToken);
        if (Instance.socket == null) {
            // Needed to force creation of the GameObject.
            var test = UnityMainThreadDispatcher.Instance;
            // Debug.Log("Connecting to socket " + AirshipPlatformUrl.gameCoordinatorSocket + " with token "+ InternalHttpManager.authToken);
            Instance.socket = new SocketIOClient.SocketIO(AirshipPlatformUrl.gameCoordinatorSocket, new SocketIOOptions() {
                Auth = new Dictionary<string, string> {
                    { "token", InternalHttpManager.authToken }
                },
                Transport = TransportProtocol.WebSocket,
                Reconnection = false
            });
            Instance.socket.JsonSerializer = new NewtonsoftJsonSerializer();
            LuauCore.onResetInstance += LuauCore_OnResetInstance;

            Instance.socket.OnAny((eventName, response) => {
                string data;
                if (response.Count > 0) {
                    data = response.GetValue().ToString();
                } else {
                    data = "{}";
                }
                
                // Uncomment to view all incoming socket events
                // print("[" + eventName + "]: " + data);

                if (Instance.isScriptListening) {
                    UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnEvent(eventName, data));
                } else {
                    UnityMainThreadDispatcher.Instance.Enqueue(() => Instance.queuedIncomingPackets.Add(new SocketPacket() {
                        eventName = eventName,
                        data = data
                    }));
                }
            });

            Instance.socket.OnConnected += async (sender, args) => {
                // print("Socket connected");
                if (Instance.firstConnect) {
                    Instance.firstConnect = false;
                    await EmitAsync("start-session", "");
                }
                foreach (var packet in Instance.queuedOutgoingPackets) {
                    await EmitAsync(packet.eventName, packet.data);
                }
            };

            Instance.socket.OnDisconnected += async (sender, s) => {
                // print("Socket disconnected: " + s);
                // refresh the auth token
                Instance.socket.Options.Auth = new Dictionary<string, string> {
                    { "token", InternalHttpManager.authToken }
                };

                await Awaitable.MainThreadAsync();
                Instance.OnDisconnected?.Invoke(s);
            };

            Instance.socket.OnError += async (sender, s) => {
                if (s.StartsWith("User does not have \"GC Edge\" access")) {
                    Debug.Log("User does not have \"GC Edge\" access");
                    await Awaitable.MainThreadAsync();
                    CrossSceneState.kickForceLogout = true;
                    TransferManager.Instance.Disconnect(true, "User does not have permission to access Airship");
                }
            };

        }

        if (!Instance.socket.Connected) {
            try {
                // refresh the auth token
                Instance.socket.Options.Auth = new Dictionary<string, string> {
                    { "token", InternalHttpManager.authToken }
                };
                await Instance.socket.ConnectAsync();
                await Awaitable.MainThreadAsync();
            } catch (Exception e) {
                Debug.LogError(e);
                return false;
            }
        }

        return Instance.socket.Connected;
    }

    public static async Task Disconnect() {
        if (Instance.socket != null) {
            // try {
            //     await Instance.socket.EmitAsync("disconnect-intent");
            // } catch (Exception e) {
            //     Debug.LogError(e);
            // }
            await Instance.socket.DisconnectAsync();
            Instance.socket = null;
        }
        Instance.firstConnect = true;
    }

    private async void OnDisable() {
        // await SendDisconnectIntent();
        LuauCore.onResetInstance -= LuauCore_OnResetInstance;
    }

    private IEnumerator FireOnEvent(string eventName, string data) {
        Instance.OnEvent?.Invoke(eventName, data);
        yield return null;
    }

    private static void LuauCore_OnResetInstance(LuauContext context) {
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